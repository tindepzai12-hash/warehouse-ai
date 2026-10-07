const crypto = require('node:crypto');
const fs = require('node:fs');
const path = require('node:path');

const schema = fs.readFileSync(path.join(__dirname, 'schema.sql'), 'utf8');
const id = () => crypto.randomUUID();
const now = () => new Date().toISOString();

function passwordHash(password) {
  const salt = crypto.randomBytes(16).toString('hex');
  const hash = crypto.scryptSync(password, salt, 64).toString('hex');
  return `scrypt:${salt}:${hash}`;
}
function verifyPassword(password, stored) {
  if (typeof stored !== 'string') return false;
  const [algorithm, salt, hash] = stored.split(':');
  if (algorithm !== 'scrypt' || !/^[a-f0-9]{32}$/.test(salt || '') || !/^[a-f0-9]{128}$/.test(hash || '')) return false;
  const candidate = crypto.scryptSync(password, salt, 64);
  return crypto.timingSafeEqual(candidate, Buffer.from(hash, 'hex'));
}

class OmsStore {
  constructor(base) {
    this.base = base;
    this.writeQueue = Promise.resolve();
    this.ready = null;
    this.synthetic = false;
  }
  run(sql, params = []) { return this.base.run(sql, params); }
  get(sql, params = []) { return this.base.get(sql, params); }
  all(sql, params = []) { return this.base.all(sql, params); }
  async exec(sql) {
    await this.base.ready;
    return new Promise((resolve, reject) => this.base.db.exec(sql, (error) => error ? reject(error) : resolve()));
  }
  async init() {
    if (this.ready) return this.ready;
    this.ready = (async () => {
      await this.exec(schema);
      const columns = await this.all('PRAGMA table_info(oms_users)');
      if (!columns.some(column => column.name === 'is_admin'))
        await this.exec('ALTER TABLE oms_users ADD COLUMN is_admin INTEGER NOT NULL DEFAULT 0 CHECK(is_admin IN (0,1))');
      await this.exec('CREATE UNIQUE INDEX IF NOT EXISTS oms_one_admin ON oms_users(is_admin) WHERE is_admin=1');
      if (process.env.NODE_ENV === 'test' || process.env.OMS_SEED_DEMO === 'true') await this.seedDevelopment();
      this.synthetic = Boolean(await this.get("SELECT 1 FROM oms_inventory_movements WHERE source_type='DEV_SEED' LIMIT 1"));
    })();
    return this.ready;
  }
  async transaction(work) {
    const execute = async () => {
      await this.init();
      await this.exec('BEGIN IMMEDIATE');
      try {
        const result = await work();
        await this.exec('COMMIT');
        return result;
      } catch (error) {
        try { await this.exec('ROLLBACK'); } catch (_) {}
        throw error;
      }
    };
    const next = this.writeQueue.then(execute, execute);
    this.writeQueue = next.catch(() => {});
    return next;
  }
  async seedDevelopment() {
    const demoPassword=process.env.OMS_DEMO_PASSWORD;
    if (!demoPassword) return;
    if (demoPassword.length<6 || demoPassword.length>256) throw new Error('OMS_DEMO_PASSWORD must contain 6-256 characters');
    const stamp = now();
    const user = await this.get('SELECT id FROM oms_users WHERE username=?', ['JustinandJarVis']);
    if (!user) {
      await this.run('INSERT INTO oms_users(id,username,password_hash,display_name,role,language,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?)',
        ['oms-manager','JustinandJarVis',passwordHash(demoPassword),'JustinandJarVis','MANAGER','vi',stamp,stamp]);
    }
    await this.run("UPDATE oms_users SET is_admin=1 WHERE id='oms-manager' AND is_admin=0");
    const worker=await this.get('SELECT id FROM oms_users WHERE username=?',['NV-00101']);
    if (!worker) {
      await this.run('INSERT INTO oms_users(id,username,password_hash,display_name,role,employee_id,language,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?)',
        ['oms-worker-101','NV-00101',passwordHash(demoPassword),'NV-00101','EMPLOYEE','emp-101','vi',stamp,stamp]);
    }
    const employees = [
      ['emp-101','NV-00101','Lan Nguyễn','RECEIVING','ONLINE','A'],
      ['emp-102','NV-00102','Minh Trần','PICKING','ONLINE','B'],
      ['emp-103','NV-00103','Hà Lê','COUNTING','ONLINE','D'],
      ['emp-104','NV-00104','Quốc Huy','PICKING','OFFLINE','C']
    ];
    for (const [eid, code, name, role, status, zone] of employees) {
      await this.run('INSERT OR IGNORE INTO oms_employees(id,employee_code,name,role,status,warehouse_zone,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?)',
        [eid,code,name,role,status,zone,stamp,stamp]);
    }
    await this.run("UPDATE oms_employees SET user_id='oms-worker-101' WHERE id='emp-101' AND user_id IS NULL");
    const products = [
      ['prod-a','SKU-A001','Coca Cola 330ml','8938500000012','lon'],
      ['prod-b','SKU-B002','Sữa UHT 1L','8938500000029','hộp'],
      ['prod-c','SKU-C003','Mì gói vị bò','8938500000036','gói']
    ];
    for (const [pid, sku, name, barcode, unit] of products) {
      await this.run('INSERT OR IGNORE INTO oms_products(id,sku,name,barcode,unit,created_at,updated_at) VALUES(?,?,?,?,?,?,?)',
        [pid,sku,name,barcode,unit,stamp,stamp]);
    }
    for (const [lid, code, zone] of [['loc-a12','A-12','A'],['loc-a17','A-17','A'],['loc-b03','B-03','B'],['loc-c02','C-02','C'],['loc-d01','D-01','D']]) {
      await this.run('INSERT OR IGNORE INTO oms_locations(id,code,zone) VALUES(?,?,?)',[lid,code,zone]);
    }
    const lots = [
      ['lot-a1','prod-a','LOT-A2401','2026-09-01',null],
      ['lot-a2','prod-a','LOT-A2402','2026-09-15',null],
      ['lot-b1','prod-b','LOT-B2401','2026-09-03','2027-02-18'],
      ['lot-b2','prod-b','LOT-B2402','2026-09-18','2027-03-20'],
      ['lot-c1','prod-c','LOT-C2401','2026-08-01','2026-10-18']
    ];
    for (const [lid, pid, code, received, expiry] of lots) {
      await this.run('INSERT OR IGNORE INTO oms_lots(id,product_id,lot_code,received_at,expiry_date) VALUES(?,?,?,?,?)',
        [lid,pid,code,received,expiry]);
    }
    for (const [bid,pid,lid,loc,qty,reserved,damaged] of [
      ['bal-a1','prod-a','lot-a1','loc-a12',120,0,0],
      ['bal-a2','prod-a','lot-a2','loc-a17',80,0,0],
      ['bal-b1','prod-b','lot-b1','loc-b03',140,20,0],
      ['bal-b2','prod-b','lot-b2','loc-b03',100,0,0],
      ['bal-c1','prod-c','lot-c1','loc-c02',96,0,4]
    ]) {
      await this.run('INSERT OR IGNORE INTO oms_balances(id,product_id,lot_id,location_id,quantity_on_hand,quantity_reserved,quantity_damaged,updated_at) VALUES(?,?,?,?,?,?,?,?)',
        [bid,pid,lid,loc,qty,reserved,damaged,stamp]);
      await this.run(`INSERT OR IGNORE INTO oms_inventory_movements
        (id,request_id,product_id,lot_id,location_id,movement_type,source_type,source_id,quantity_change,quantity_before,quantity_after,actor_user_id,created_at)
        VALUES(?,?,?,?,?,'OPENING_BALANCE','DEV_SEED',?,?,?,?,?,?)`,
        [`opening-${bid}`,`seed-opening-${bid}`,pid,lid,loc,bid,qty,0,qty,'oms-manager',stamp]);
    }
    for (const [oid,code,ref,status] of [
      ['ord-1','ORD-00125','Shopee · SHOP-701','NEW'],
      ['ord-2','ORD-00126','TikTok Shop · SHOP-811','PICKING'],
      ['ord-3','ORD-00127','Lazada · SHOP-621','SHIPPED']
    ]) {
      await this.run('INSERT OR IGNORE INTO oms_orders(id,order_code,customer_reference,status,created_at,updated_at) VALUES(?,?,?,?,?,?)',[oid,code,ref,status,stamp,stamp]);
    }
    for (const [iid,oid,pid,qty] of [['oi-1','ord-1','prod-a',4],['oi-2','ord-2','prod-b',12],['oi-3','ord-3','prod-c',8]]) {
      await this.run('INSERT OR IGNORE INTO oms_order_items(id,order_id,product_id,quantity) VALUES(?,?,?,?)',[iid,oid,pid,qty]);
    }
    await this.run('INSERT OR IGNORE INTO oms_returns(id,return_code,order_id,status,reason,created_at,received_at,employee_id,notes) VALUES(?,?,?,?,?,?,?,?,?)',
      ['ret-1','RET-00125','ord-3','INSPECTING','Bao bì bị móp',stamp,stamp,'emp-101','Chờ xác nhận tình trạng hàng']);
    await this.run('INSERT OR IGNORE INTO oms_return_items(id,return_id,order_item_id,product_id,lot_id,quantity,condition) VALUES(?,?,?,?,?,?,?)',
      ['ri-1','ret-1','oi-3','prod-c','lot-c1',2,'UNINSPECTED']);
    for (const [rid,code,pid,lid,loc,expected,received,shortage,status] of [
      ['rec-1','RCV-00101','prod-b','lot-b1','loc-b03',100,96,4,'SHORTAGE'],
      ['rec-2','RCV-00102','prod-a','lot-a2','loc-a17',80,80,0,'COMPLETE'],
      ['rec-3','RCV-00103','prod-c','lot-c1','loc-c02',50,0,0,'PENDING']
    ]) {
      await this.run('INSERT OR IGNORE INTO oms_receivings(id,receiving_code,product_id,lot_id,location_id,expected_quantity,received_quantity,shortage_quantity,status,created_at,completed_at) VALUES(?,?,?,?,?,?,?,?,?,?,?)',
        [rid,code,pid,lid,loc,expected,received,shortage,status,stamp,status==='PENDING'?null:stamp]);
    }
    for (const [tid,code,type,title,status,eid,pid,loc,qty] of [
      ['task-1','TASK-001','RECEIVING','Kiểm đếm sữa UHT','ASSIGNED','emp-101','prod-b','loc-b03',100],
      ['task-2','TASK-002','PICKING','Nhặt Coca Cola cho đơn ORD-00125','IN_PROGRESS','emp-102','prod-a','loc-a12',4],
      ['task-3','TASK-003','COUNTING','Kiểm kê kệ D','UNASSIGNED',null,null,'loc-d01',null]
    ]) {
      await this.run('INSERT OR IGNORE INTO oms_tasks(id,task_code,task_type,title,status,employee_id,product_id,location_id,expected_quantity,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?,?,?)',
        [tid,code,type,title,status,eid,pid,loc,qty,stamp,stamp]);
    }
    await this.run('INSERT OR IGNORE INTO oms_conversations(id,employee_id,created_at,updated_at) VALUES(?,?,?,?)',
      ['conv-101','emp-101',stamp,stamp]);
    await this.run('INSERT OR IGNORE INTO oms_chat_messages(id,conversation_id,sender_user_id,content,created_at) VALUES(?,?,?,?,?)',
      ['msg-seed-1','conv-101','oms-manager','Kiểm tra số thực nhận và báo lại nếu thiếu hàng.',stamp]);
  }
  async userByUsername(username) {
    return this.get('SELECT id,username,password_hash,display_name,role,employee_id,language,is_admin FROM oms_users WHERE username=?',[username]);
  }
  async createInitialAdmin(username, password) {
    if (!/^[A-Za-z0-9._-]{3,80}$/.test(username) || typeof password !== 'string' || password.length < 16 || password.length > 256)
      throw new Error('Admin username or password is invalid');
    return this.createInitialAdminHash(username,passwordHash(password));
  }
  // Opt-in cloud bootstrap stores a server-side hash; never reset an existing admin.
  async createInitialAdminHash(username, hashedPassword, allowExisting=false) {
    if (!/^[A-Za-z0-9._-]{3,80}$/.test(username)||!/^scrypt:[a-f0-9]{32}:[a-f0-9]{128}$/.test(hashedPassword||''))
      throw new Error('Admin bootstrap configuration is invalid');
    return this.transaction(async () => {
      if (await this.get('SELECT 1 FROM oms_users WHERE is_admin=1 LIMIT 1')) {
        if(allowExisting)return {created:false};
        throw new Error('An admin account already exists');
      }
      const userId = id();
      const stamp = now();
      await this.run('INSERT INTO oms_users(id,username,password_hash,display_name,role,is_admin,language,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?)',
        [userId,username,hashedPassword,'Administrator','MANAGER',1,'vi',stamp,stamp]);
      return { id:userId, username };
    });
  }
  async createAccount(data, actorId) {
    return this.transaction(async () => {
      if (await this.userByUsername(data.username)) {
        const error = new Error('Account name is already in use');
        error.status = 409;
        throw error;
      }
      const stamp = now();
      const userId = id();
      const employeeId = data.role === 'EMPLOYEE' ? id() : null;
      await this.run('INSERT INTO oms_users(id,username,password_hash,display_name,role,is_admin,employee_id,language,created_at,updated_at) VALUES(?,?,?,?,?,0,?,?,?,?)',
        [userId,data.username,passwordHash(data.password),data.displayName,data.role,employeeId,'vi',stamp,stamp]);
      if (employeeId) await this.run('INSERT INTO oms_employees(id,employee_code,user_id,name,role,status,warehouse_zone,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?,?)',
        [employeeId,data.username,userId,data.displayName,data.workRole,'OFFLINE',data.warehouseZone||null,stamp,stamp]);
      await this.audit(actorId,'ACCOUNT_CREATED','USER',userId,null,{username:data.username,role:data.role,employeeId});
      return {id:userId,username:data.username,displayName:data.displayName,role:data.role,canEdit:false,employeeId};
    });
  }
  async accounts() {
    return this.all('SELECT id,username,display_name AS displayName,role,is_admin AS isAdmin,employee_id AS employeeId FROM oms_users ORDER BY created_at');
  }
  async sessionByToken(token) {
    if (!token || typeof token !== 'string') return null;
    const hash = crypto.createHash('sha256').update(token).digest('hex');
    return this.get(`SELECT s.csrf_token,s.expires_at,u.id,u.username,u.display_name,u.role,u.employee_id,u.language,u.is_admin
      FROM oms_sessions s JOIN oms_users u ON u.id=s.user_id WHERE s.token_hash=? AND s.expires_at>?`,[hash,now()]);
  }
  async createSession(userId) {
    const token = crypto.randomBytes(32).toString('base64url');
    const csrf = crypto.randomBytes(24).toString('base64url');
    const expires = new Date(Date.now()+8*60*60*1000).toISOString();
    await this.run('INSERT INTO oms_sessions(token_hash,user_id,csrf_token,expires_at,created_at) VALUES(?,?,?,?,?)',
      [crypto.createHash('sha256').update(token).digest('hex'),userId,csrf,expires,now()]);
    return { token, csrf, expires };
  }
  async deleteSession(token) {
    const hash = crypto.createHash('sha256').update(token).digest('hex');
    await this.run('DELETE FROM oms_sessions WHERE token_hash=?',[hash]);
  }
  async createMobileSession(userId) {
    const token=crypto.randomBytes(32).toString('base64url');
    const expiresAt=new Date(Date.now()+8*60*60*1000).toISOString();
    const tokenHash=crypto.createHash('sha256').update(token).digest('hex');
    await this.run('INSERT INTO oms_mobile_sessions(token_hash,user_id,expires_at,created_at) VALUES(?,?,?,?)',
      [tokenHash,userId,expiresAt,now()]);
    return {token,expiresAt};
  }
  async mobileSessionByToken(token) {
    if(!token||typeof token!=='string') return null;
    const tokenHash=crypto.createHash('sha256').update(token).digest('hex');
    return this.get(`SELECT u.id,u.username,u.display_name,u.role,u.employee_id,u.language,u.is_admin
      FROM oms_mobile_sessions s JOIN oms_users u ON u.id=s.user_id
      WHERE s.token_hash=? AND s.expires_at>?`,[tokenHash,now()]);
  }
  async deleteMobileSession(token) {
    const tokenHash=crypto.createHash('sha256').update(token).digest('hex');
    await this.run('DELETE FROM oms_mobile_sessions WHERE token_hash=?',[tokenHash]);
  }
  async changeOwnPassword(userId,currentPassword,newPassword) {
    return this.transaction(async()=>{
      const user=await this.get('SELECT password_hash FROM oms_users WHERE id=?',[userId]);
      if(!user||!verifyPassword(currentPassword,user.password_hash)) throw Object.assign(new Error('Current password is incorrect'),{status:422});
      if(verifyPassword(newPassword,user.password_hash)) throw Object.assign(new Error('New password must be different'),{status:422});
      await this.run('UPDATE oms_users SET password_hash=?,updated_at=? WHERE id=?',[passwordHash(newPassword),now(),userId]);
      await this.run('DELETE FROM oms_sessions WHERE user_id=?',[userId]);
      await this.run('DELETE FROM oms_mobile_sessions WHERE user_id=?',[userId]);
    });
  }
  async dashboard() {
    const [orders,returns,tasks,inventory] = await Promise.all([
      this.get("SELECT COUNT(*) AS count FROM oms_orders WHERE status='NEW'"),
      this.get("SELECT COUNT(*) AS count FROM oms_returns WHERE status NOT IN ('CLOSED','RESTOCKED')"),
      this.get("SELECT COUNT(*) AS count FROM oms_tasks WHERE status NOT IN ('COMPLETED')"),
      this.get('SELECT COALESCE(SUM(quantity_on_hand-quantity_reserved-quantity_damaged),0) AS count FROM oms_balances')
    ]);
    return { newOrders: orders.count, activeReturns: returns.count, openTasks: tasks.count, availableUnits: inventory.count, synthetic: this.synthetic, updatedAt: now() };
  }
  async orders(query = '') {
    const term = `%${query.trim()}%`;
    const rows = await this.all('SELECT * FROM oms_orders WHERE order_code LIKE ? OR customer_reference LIKE ? ORDER BY created_at DESC LIMIT 100',[term,term]);
    for (const row of rows) row.items = await this.all(`SELECT i.id AS orderItemId,p.id AS productId,p.sku,p.name,p.barcode,i.quantity
      FROM oms_order_items i JOIN oms_products p ON p.id=i.product_id WHERE i.order_id=?`,[row.id]);
    return rows;
  }
  async order(idOrCode) {
    const row = await this.get('SELECT * FROM oms_orders WHERE id=? OR order_code=?',[idOrCode,idOrCode]);
    if (!row) return null;
    row.items = await this.all(`SELECT i.id AS orderItemId,p.id AS productId,p.sku,p.name,p.barcode,i.quantity
      FROM oms_order_items i JOIN oms_products p ON p.id=i.product_id WHERE i.order_id=?`,[row.id]);
    return row;
  }
  async returns(query = '') {
    const term = `%${query.trim()}%`;
    return this.all(`SELECT r.*,o.order_code,COALESCE(SUM(i.quantity),0) AS item_quantity
      FROM oms_returns r JOIN oms_orders o ON o.id=r.order_id
      LEFT JOIN oms_return_items i ON i.return_id=r.id
      WHERE r.return_code LIKE ? OR o.order_code LIKE ? OR EXISTS(
        SELECT 1 FROM oms_return_items ri JOIN oms_products p ON p.id=ri.product_id
        WHERE ri.return_id=r.id AND p.barcode LIKE ?)
      GROUP BY r.id ORDER BY r.created_at DESC LIMIT 100`,[term,term,term]);
  }
  async returnDetail(idOrCode) {
    const row = await this.get('SELECT r.*,o.order_code FROM oms_returns r JOIN oms_orders o ON o.id=r.order_id WHERE r.id=? OR r.return_code=?',[idOrCode,idOrCode]);
    if (!row) return null;
    row.items = await this.all(`SELECT ri.*,p.sku,p.name,p.barcode,l.lot_code FROM oms_return_items ri
      JOIN oms_products p ON p.id=ri.product_id LEFT JOIN oms_lots l ON l.id=ri.lot_id WHERE ri.return_id=?`,[row.id]);
    return row;
  }
  async createReturn(data,actorId) {
    return this.transaction(async()=>{
      const item=await this.get('SELECT order_id,product_id,quantity FROM oms_order_items WHERE id=?',[data.orderItemId]);
      if(!item||item.order_id!==data.orderId||data.quantity>item.quantity)return null;
      const order=await this.get('SELECT status FROM oms_orders WHERE id=?',[data.orderId]);
      if(order?.status!=='SHIPPED'){const error=new Error('Only shipped orders can be returned');error.status=409;throw error;}
      const earlier=await this.get(`SELECT COALESCE(SUM(ri.quantity),0) AS quantity FROM oms_return_items ri
        JOIN oms_returns r ON r.id=ri.return_id WHERE ri.order_item_id=?`,[data.orderItemId]);
      if(earlier.quantity+data.quantity>item.quantity){const error=new Error('Return quantity exceeds shipped item quantity');error.status=422;throw error;}
      const returnId=id();
      const code=`RET-${String(Date.now()).slice(-8)}-${crypto.randomBytes(2).toString('hex').toUpperCase()}`;
      await this.run(`INSERT INTO oms_returns(id,return_code,order_id,status,reason,created_at,notes)
        VALUES(?,?,?,'REQUESTED',?,?,?)`,[returnId,code,data.orderId,data.reason,now(),data.notes||null]);
      await this.run('INSERT INTO oms_return_items(id,return_id,order_item_id,product_id,quantity,condition) VALUES(?,?,?,?,?,?)',
        [id(),returnId,data.orderItemId,item.product_id,data.quantity,'UNINSPECTED']);
      const value=await this.returnDetail(returnId);
      await this.audit(actorId,'RETURN_CREATED','RETURN',returnId,null,value);
      return value;
    });
  }
  async advanceReturn(idOrCode,toStatus,actorId) {
    const allowed={REQUESTED:['IN_TRANSIT','RECEIVED'],IN_TRANSIT:['RECEIVED'],RECEIVED:['INSPECTING']};
    return this.transaction(async()=>{
      const value=await this.returnDetail(idOrCode);
      if(!value)return null;
      if(!(allowed[value.status]||[]).includes(toStatus)){const error=new Error('Invalid return status transition');error.status=409;throw error;}
      await this.run('UPDATE oms_returns SET status=?,received_at=COALESCE(received_at,?) WHERE id=?',
        [toStatus,toStatus==='RECEIVED'||toStatus==='INSPECTING'?now():null,value.id]);
      const updated=await this.returnDetail(value.id);
      await this.audit(actorId,`RETURN_${toStatus}`,'RETURN',value.id,value,updated);
      return updated;
    });
  }
  async restockReturn(data,actorId) {
    return this.transaction(async()=>{
      const value=await this.returnDetail(data.returnId);
      if(!value)return null;
      const prior=await this.get('SELECT id,source_id AS sourceId,lot_id AS lotId,location_id AS locationId,quantity_change AS quantityChange FROM oms_inventory_movements WHERE request_id=?',[data.requestId]);
      if(prior){
        if(prior.sourceId!==value.id||prior.lotId!==data.lotId||prior.locationId!==data.locationId||prior.quantityChange!==data.quantity){const error=new Error('Request ID belongs to another return payload');error.status=409;throw error;}
        return value;
      }
      if(value.status!=='INSPECTING'){const error=new Error('Return must be inspected before restock');error.status=409;throw error;}
      const item=value.items.find(row=>row.id===data.returnItemId);
      const lot=await this.get('SELECT product_id FROM oms_lots WHERE id=?',[data.lotId]);
      const location=await this.get('SELECT id FROM oms_locations WHERE id=?',[data.locationId]);
      if(!item||!lot||!location||lot.product_id!==item.product_id||data.quantity!==item.quantity){const error=new Error('Return item, lot, location or quantity is invalid; partial restock is not enabled');error.status=422;throw error;}
      if(item.disposition){const error=new Error('Return item was already processed');error.status=409;throw error;}
      const balanceId=`${item.product_id}:${data.lotId}:${data.locationId}`;
      await this.run('INSERT OR IGNORE INTO oms_balances(id,product_id,lot_id,location_id,quantity_on_hand,quantity_reserved,quantity_damaged,updated_at) VALUES(?,?,?,?,0,0,0,?)',
        [balanceId,item.product_id,data.lotId,data.locationId,now()]);
      const balance=await this.get('SELECT * FROM oms_balances WHERE product_id=? AND lot_id=? AND location_id=?',[item.product_id,data.lotId,data.locationId]);
      const next=balance.quantity_on_hand+data.quantity;
      await this.run('UPDATE oms_balances SET quantity_on_hand=?,updated_at=? WHERE id=?',[next,now(),balance.id]);
      await this.run("UPDATE oms_return_items SET lot_id=?,condition='SELLABLE',disposition='RESTOCK' WHERE id=?",[data.lotId,item.id]);
      const remaining=await this.get("SELECT COUNT(*) AS count FROM oms_return_items WHERE return_id=? AND disposition IS NULL",[value.id]);
      if(remaining.count===0)await this.run("UPDATE oms_returns SET status='RESTOCKED' WHERE id=?",[value.id]);
      await this.run(`INSERT INTO oms_inventory_movements(id,request_id,product_id,lot_id,location_id,movement_type,source_type,source_id,quantity_change,quantity_before,quantity_after,actor_user_id,created_at)
        VALUES(?,?,?,?,?,'RETURN_RESTOCK','RETURN',?,?,?,?,?,?)`,
        [id(),data.requestId,item.product_id,data.lotId,data.locationId,value.id,data.quantity,balance.quantity_on_hand,next,actorId,now()]);
      const updated=await this.returnDetail(value.id);
      await this.audit(actorId,'RETURN_RESTOCKED','RETURN',value.id,value,updated);
      return updated;
    });
  }
  async inventory(query = '') {
    const term = `%${query.trim()}%`;
    return this.all(`SELECT p.id AS productId,p.sku,p.name,p.barcode,p.unit,
      l.id AS lotId,l.lot_code AS lotCode,l.received_at AS receivedAt,l.expiry_date AS expiryDate,
      loc.id AS locationId,loc.code AS locationCode,b.quantity_on_hand AS onHand,
      b.quantity_reserved AS reserved,b.quantity_damaged AS damaged,
      b.quantity_on_hand-b.quantity_reserved-b.quantity_damaged AS available
      FROM oms_balances b JOIN oms_products p ON p.id=b.product_id JOIN oms_lots l ON l.id=b.lot_id
      JOIN oms_locations loc ON loc.id=b.location_id
      WHERE p.barcode LIKE ? OR p.sku LIKE ? OR p.name LIKE ? OR l.lot_code LIKE ? OR loc.code LIKE ?
      ORDER BY p.sku,l.expiry_date IS NULL,l.expiry_date,l.received_at LIMIT 200`,
      [term,term,term,term,term]);
  }
  async products(query = '') {
    const term = `%${query.trim()}%`;
    return this.all('SELECT id,sku,name,barcode,unit,lot_tracking_required AS lotTrackingRequired FROM oms_products WHERE sku LIKE ? OR barcode LIKE ? OR name LIKE ? ORDER BY sku LIMIT 100',
      [term,term,term]);
  }
  async createProduct(data,actorId) {
    return this.transaction(async()=>{
      const productId=id();
      await this.run('INSERT INTO oms_products(id,sku,name,barcode,unit,lot_tracking_required,created_at,updated_at) VALUES(?,?,?,?,?,?,?,?)',
        [productId,data.sku,data.name,data.barcode,data.unit,data.lotTrackingRequired?1:0,now(),now()]);
      const product=await this.get('SELECT * FROM oms_products WHERE id=?',[productId]);
      await this.audit(actorId,'PRODUCT_CREATED','PRODUCT',productId,null,product);
      return product;
    });
  }
  async locations() {
    return this.all('SELECT id,code,zone FROM oms_locations ORDER BY code');
  }
  async createLocation(data,actorId) {
    return this.transaction(async()=>{
      const locationId=id();
      await this.run('INSERT INTO oms_locations(id,code,zone) VALUES(?,?,?)',[locationId,data.code,data.zone]);
      const location=await this.get('SELECT * FROM oms_locations WHERE id=?',[locationId]);
      await this.audit(actorId,'LOCATION_CREATED','LOCATION',locationId,null,location);
      return location;
    });
  }
  async lots(productId = null) {
    return this.all(`SELECT l.id,l.product_id AS productId,l.lot_code AS lotCode,l.received_at AS receivedAt,
      l.expiry_date AS expiryDate,p.sku,p.barcode FROM oms_lots l JOIN oms_products p ON p.id=l.product_id
      WHERE (? IS NULL OR l.product_id=?) ORDER BY l.received_at DESC LIMIT 200`,[productId,productId]);
  }
  async createLot(data,actorId) {
    return this.transaction(async()=>{
      const product=await this.get('SELECT id FROM oms_products WHERE id=?',[data.productId]);
      if(!product)return null;
      const lotId=id();
      await this.run('INSERT INTO oms_lots(id,product_id,lot_code,received_at,expiry_date) VALUES(?,?,?,?,?)',
        [lotId,data.productId,data.lotCode,data.receivedAt||now().slice(0,10),data.expiryDate||null]);
      const lot=await this.get('SELECT * FROM oms_lots WHERE id=?',[lotId]);
      await this.audit(actorId,'LOT_CREATED','LOT',lotId,null,lot);
      return lot;
    });
  }
  async pickSuggestion(productId) {
    const rows = await this.all(`SELECT l.id AS lotId,l.lot_code AS lotCode,l.expiry_date AS expiryDate,l.received_at AS receivedAt,
      loc.code AS locationCode,b.quantity_on_hand-b.quantity_reserved-b.quantity_damaged AS available
      FROM oms_balances b JOIN oms_lots l ON l.id=b.lot_id JOIN oms_locations loc ON loc.id=b.location_id
      WHERE b.product_id=? AND b.quantity_on_hand>b.quantity_reserved+b.quantity_damaged
      ORDER BY l.expiry_date IS NULL,l.expiry_date,l.received_at`,[productId]);
    return rows.map((row,index)=>({ ...row, priority:index+1, policy:row.expiryDate?'FEFO':'FIFO' }));
  }
  async receivings() {
    return this.all(`SELECT r.*,p.sku,p.name,p.barcode,l.lot_code AS lotCode,loc.code AS locationCode
      FROM oms_receivings r JOIN oms_products p ON p.id=r.product_id
      JOIN oms_lots l ON l.id=r.lot_id JOIN oms_locations loc ON loc.id=r.location_id
      ORDER BY r.created_at DESC LIMIT 100`);
  }
  async createReceiving(data,actorId) {
    return this.transaction(async()=>{
      const lot=await this.get('SELECT product_id FROM oms_lots WHERE id=?',[data.lotId]);
      const location=await this.get('SELECT id FROM oms_locations WHERE id=?',[data.locationId]);
      if(!lot||!location||lot.product_id!==data.productId)return null;
      const receivingId=id();
      const code=`RCV-${String(Date.now()).slice(-8)}-${crypto.randomBytes(2).toString('hex').toUpperCase()}`;
      await this.run(`INSERT INTO oms_receivings(id,receiving_code,product_id,lot_id,location_id,expected_quantity,status,created_at)
        VALUES(?,?,?,?,?,?,'PENDING',?)`,[receivingId,code,data.productId,data.lotId,data.locationId,data.expectedQuantity,now()]);
      const value=await this.get('SELECT * FROM oms_receivings WHERE id=?',[receivingId]);
      await this.audit(actorId,'RECEIVING_CREATED','RECEIVING',receivingId,null,value);
      return value;
    });
  }
  async confirmReceiving(receivingId,actualQuantity,requestId,actorId) {
    return this.transaction(async()=>{
      const prior=await this.get('SELECT id,source_id AS sourceId,quantity_change AS quantityChange FROM oms_inventory_movements WHERE request_id=?',[requestId]);
      if (prior) {
        if (prior.sourceId!==receivingId || prior.quantityChange!==actualQuantity) { const error=new Error('Request ID belongs to another receiving payload'); error.status=409; throw error; }
        return this.get('SELECT * FROM oms_receivings WHERE id=?',[receivingId]);
      }
      const receiving=await this.get('SELECT * FROM oms_receivings WHERE id=?',[receivingId]);
      if (!receiving) return null;
      if (receiving.status!=='PENDING') { const error=new Error('Receiving is already completed'); error.status=409; throw error; }
      if (actualQuantity>receiving.expected_quantity) { const error=new Error('Over-receiving requires a separate approval flow'); error.status=422; throw error; }
      const shortage=Math.max(receiving.expected_quantity-actualQuantity,0);
      const balanceId=`${receiving.product_id}:${receiving.lot_id}:${receiving.location_id}`;
      await this.run('INSERT OR IGNORE INTO oms_balances(id,product_id,lot_id,location_id,quantity_on_hand,quantity_reserved,quantity_damaged,updated_at) VALUES(?,?,?,?,0,0,0,?)',
        [balanceId,receiving.product_id,receiving.lot_id,receiving.location_id,now()]);
      const balance=await this.get('SELECT * FROM oms_balances WHERE product_id=? AND lot_id=? AND location_id=?',
        [receiving.product_id,receiving.lot_id,receiving.location_id]);
      const newQuantity=balance.quantity_on_hand+actualQuantity;
      await this.run('UPDATE oms_balances SET quantity_on_hand=?,updated_at=? WHERE id=?',[newQuantity,now(),balance.id]);
      await this.run(`UPDATE oms_receivings SET received_quantity=?,shortage_quantity=?,status=?,completed_at=? WHERE id=?`,
        [actualQuantity,shortage,shortage>0?'SHORTAGE':'COMPLETE',now(),receivingId]);
      await this.run(`INSERT INTO oms_inventory_movements(id,request_id,product_id,lot_id,location_id,movement_type,source_type,source_id,quantity_change,quantity_before,quantity_after,actor_user_id,created_at)
        VALUES(?,?,?,?,?,'INBOUND','RECEIVING',?,?,?,?,?,?)`,
        [id(),requestId,receiving.product_id,receiving.lot_id,receiving.location_id,receiving.id,actualQuantity,balance.quantity_on_hand,newQuantity,actorId,now()]);
      const updated=await this.get('SELECT * FROM oms_receivings WHERE id=?',[receivingId]);
      await this.audit(actorId,'RECEIVING_COMPLETED','RECEIVING',receivingId,receiving,updated);
      return updated;
    });
  }
  async tasks(employeeId = null) {
    return this.all(`SELECT t.*,e.employee_code AS employeeCode,p.sku,p.name AS productName,p.barcode,p.unit,loc.code AS locationCode
      FROM oms_tasks t LEFT JOIN oms_employees e ON e.id=t.employee_id
      LEFT JOIN oms_products p ON p.id=t.product_id LEFT JOIN oms_locations loc ON loc.id=t.location_id
      WHERE (? IS NULL OR t.employee_id=?) ORDER BY t.created_at DESC LIMIT 100`,[employeeId,employeeId]);
  }
  async task(idOrCode) {
    return this.get('SELECT * FROM oms_tasks WHERE id=? OR task_code=?',[idOrCode,idOrCode]);
  }
  async taskTransition(taskId,toStatus,actorId,requestId=null) {
    const allowed={ ASSIGNED:['ACCEPTED','BLOCKED'], ACCEPTED:['IN_PROGRESS','BLOCKED'], IN_PROGRESS:['BLOCKED','COMPLETED'], BLOCKED:['IN_PROGRESS'] };
    return this.transaction(async()=>{
      if(requestId){
        const replay=await this.get('SELECT task_id AS taskId,response_json AS responseJson FROM oms_mobile_event_replays WHERE user_id=? AND request_id=?',[actorId,requestId]);
        if(replay){
          if(replay.taskId!==taskId){const error=new Error('Request ID belongs to another task');error.status=409;throw error;}
          const response=JSON.parse(replay.responseJson);
          if(response.status!==toStatus){const error=new Error('Request ID belongs to another status');error.status=409;throw error;}
          return response;
        }
      }
      const task=await this.task(taskId);
      if (!task) return null;
      if (!(allowed[task.status]||[]).includes(toStatus)) { const error=new Error('Invalid task status transition'); error.status=409; throw error; }
      await this.run('UPDATE oms_tasks SET status=?,updated_at=? WHERE id=?',[toStatus,now(),task.id]);
      await this.run('INSERT INTO oms_task_events(id,task_id,from_status,to_status,actor_user_id,created_at) VALUES(?,?,?,?,?,?)',
        [id(),task.id,task.status,toStatus,actorId,now()]);
      const updated=await this.task(task.id);
      await this.audit(actorId,'TASK_STATUS_CHANGED','TASK',task.id,task,updated);
      if(requestId)await this.run('INSERT INTO oms_mobile_event_replays(user_id,request_id,task_id,response_json) VALUES(?,?,?,?)',
        [actorId,requestId,task.id,JSON.stringify(updated)]);
      return updated;
    });
  }
  async employees() {
    return this.all(`SELECT e.id,e.employee_code AS employeeCode,e.name,e.role,e.status,e.warehouse_zone AS warehouseZone,
      (SELECT COUNT(*) FROM oms_tasks t WHERE t.employee_id=e.id AND t.status IN ('ASSIGNED','ACCEPTED','IN_PROGRESS')) AS openTasks
      FROM oms_employees e ORDER BY e.employee_code`);
  }
  async suggestEmployee(taskId) {
    const task = await this.task(taskId);
    if (!task) return null;
    const employees = await this.employees();
    const eligible = employees.filter(e=>e.status==='ONLINE' && e.openTasks<3);
    const ranked = eligible.map(e=>{
      let score = 1-e.openTasks*.2;
      if (task.task_type===e.role) score+=.4;
      return { employee:e, score };
    }).sort((a,b)=>b.score-a.score || a.employee.employeeCode.localeCompare(b.employee.employeeCode));
    if (!ranked.length) return null;
    const best = ranked[0].employee;
    return { employeeCode: best.employeeCode, reason: `Trực tuyến, ${best.openTasks} nhiệm vụ đang xử lý; vai trò ${best.role}.`, confidence: Math.min(.95,Math.max(.55,ranked[0].score/1.5)) };
  }
  async audit(userId, action, entityType, entityId, before, after, originatedFrom='USER') {
    await this.run('INSERT INTO oms_audit_logs(id,user_id,action,entity_type,entity_id,before_json,after_json,originated_from,created_at) VALUES(?,?,?,?,?,?,?,?,?)',
      [id(),userId,action,entityType,entityId,before?JSON.stringify(before):null,after?JSON.stringify(after):null,originatedFrom,now()]);
  }
  async assignTask(taskId, employeeCode, actorId) {
    return this.transaction(async()=>{
      const task = await this.task(taskId);
      if (!task) return null;
      if(task.status!=='UNASSIGNED'){const error=new Error('Only unassigned tasks can be assigned');error.status=409;throw error;}
      const employee = await this.get('SELECT id,status FROM oms_employees WHERE employee_code=?',[employeeCode]);
      if (!employee || employee.status!=='ONLINE') { const error=new Error('Employee must be ONLINE'); error.status=422; throw error; }
      const previous = { ...task };
      await this.run("UPDATE oms_tasks SET employee_id=?,status='ASSIGNED',updated_at=? WHERE id=?",[employee.id,now(),task.id]);
      await this.run('INSERT INTO oms_task_events(id,task_id,from_status,to_status,actor_user_id,created_at) VALUES(?,?,?,?,?,?)',
        [id(),task.id,task.status,'ASSIGNED',actorId,now()]);
      const updated=await this.task(task.id);
      await this.audit(actorId,'TASK_ASSIGNED','TASK',task.id,previous,updated);
      return updated;
    });
  }
  async createTask(data,actorId) {
    return this.transaction(async()=>{
      if(data.productId && !(await this.get('SELECT id FROM oms_products WHERE id=?',[data.productId]))) {
        const error=new Error('Product does not exist');error.status=422;throw error;
      }
      if(data.locationId && !(await this.get('SELECT id FROM oms_locations WHERE id=?',[data.locationId]))) {
        const error=new Error('Location does not exist');error.status=422;throw error;
      }
      const code=`TASK-${String(Date.now()).slice(-8)}-${crypto.randomBytes(2).toString('hex').toUpperCase()}`;
      const taskId=id();
      const title=data.title.trim();
      await this.run(`INSERT INTO oms_tasks(id,task_code,task_type,title,status,priority,source_type,source_id,product_id,location_id,expected_quantity,due_at,created_at,updated_at)
        VALUES(?,?,?,?,'UNASSIGNED',?,?,?,?,?,?,?,?,?)`,
        [taskId,code,data.taskType,title,data.priority||'NORMAL',data.sourceType||null,data.sourceId||null,data.productId||null,data.locationId||null,data.expectedQuantity??null,data.dueAt||null,now(),now()]);
      const task=await this.task(taskId);
      await this.audit(actorId,'TASK_CREATED','TASK',taskId,null,task);
      return task;
    });
  }
  async conversations(employeeId = null) {
    return this.all(`SELECT c.id,c.updated_at AS updatedAt,e.employee_code AS employeeCode,e.status,
      (SELECT content FROM oms_chat_messages m WHERE m.conversation_id=c.id ORDER BY created_at DESC LIMIT 1) AS latestMessage
      FROM oms_conversations c JOIN oms_employees e ON e.id=c.employee_id
      WHERE (? IS NULL OR c.employee_id=?) ORDER BY c.updated_at DESC`,[employeeId,employeeId]);
  }
  async conversation(idOrEmployeeCode) {
    return this.get(`SELECT c.id,c.employee_id AS employeeId,e.employee_code AS employeeCode
      FROM oms_conversations c JOIN oms_employees e ON e.id=c.employee_id
      WHERE c.id=? OR e.employee_code=?`,[idOrEmployeeCode,idOrEmployeeCode]);
  }
  async ensureConversation(employeeCode) {
    const employee=await this.get('SELECT id FROM oms_employees WHERE employee_code=?',[employeeCode]);
    if (!employee) return null;
    const existing=await this.conversation(employeeCode);
    if (existing) return existing;
    return this.transaction(async()=>{
      const fresh=await this.conversation(employeeCode);
      if (fresh) return fresh;
      const conversationId=id();
      await this.run('INSERT INTO oms_conversations(id,employee_id,created_at,updated_at) VALUES(?,?,?,?)',
        [conversationId,employee.id,now(),now()]);
      return this.conversation(conversationId);
    });
  }
  async messages(conversationId) {
    return this.all(`SELECT m.id,m.sender_user_id AS senderId,u.username AS sender,m.content,m.created_at AS createdAt
      FROM oms_chat_messages m JOIN oms_users u ON u.id=m.sender_user_id
      WHERE m.conversation_id=? ORDER BY m.created_at LIMIT 200`,[conversationId]);
  }
  async addMessage(conversationId,userId,content) {
    return this.transaction(async()=>{
      const conversation=await this.get('SELECT id FROM oms_conversations WHERE id=?',[conversationId]);
      if (!conversation) return null;
      const messageId=id();
      await this.run('INSERT INTO oms_chat_messages(id,conversation_id,sender_user_id,content,created_at) VALUES(?,?,?,?,?)',
        [messageId,conversationId,userId,content.trim(),now()]);
      await this.run('UPDATE oms_conversations SET updated_at=? WHERE id=?',[now(),conversationId]);
      await this.audit(userId,'CHAT_MESSAGE_SENT','CONVERSATION',conversationId,null,{messageId});
      return this.get('SELECT id,content,created_at AS createdAt FROM oms_chat_messages WHERE id=?',[messageId]);
    });
  }
  async setLanguage(userId,language) {
    await this.run('UPDATE oms_users SET language=?,updated_at=? WHERE id=?',[language,now(),userId]);
    await this.audit(userId,'LANGUAGE_CHANGED','USER',userId,null,{language});
  }
  async aiConversations(userId) {
    return this.all('SELECT id,title,created_at AS createdAt,updated_at AS updatedAt FROM oms_ai_conversations WHERE user_id=? ORDER BY updated_at DESC LIMIT 30',[userId]);
  }
  async aiConversation(conversationId,userId) {
    return this.get('SELECT id,title FROM oms_ai_conversations WHERE id=? AND user_id=?',[conversationId,userId]);
  }
  async createAiConversation(userId,title) {
    const conversationId=id();
    await this.run('INSERT INTO oms_ai_conversations(id,user_id,title,created_at,updated_at) VALUES(?,?,?,?,?)',
      [conversationId,userId,title.slice(0,90),now(),now()]);
    return conversationId;
  }
  async aiMessages(conversationId) {
    const rows=await this.all('SELECT id,role,content,sources_json AS sourcesJson,created_at AS createdAt FROM oms_ai_messages WHERE conversation_id=? ORDER BY created_at LIMIT 100',[conversationId]);
    return rows.map(row=>({id:row.id,role:row.role,content:row.content,sources:row.sourcesJson?JSON.parse(row.sourcesJson):[],createdAt:row.createdAt}));
  }
  async addAiMessage(conversationId,role,content,sources=[]) {
    await this.run('INSERT INTO oms_ai_messages(id,conversation_id,role,content,sources_json,created_at) VALUES(?,?,?,?,?,?)',
      [id(),conversationId,role,content,JSON.stringify(sources),now()]);
    await this.run('UPDATE oms_ai_conversations SET updated_at=? WHERE id=?',[now(),conversationId]);
  }
}

module.exports = { OmsStore, passwordHash, verifyPassword };
