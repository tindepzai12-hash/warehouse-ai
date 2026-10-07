const express = require('express');
const crypto = require('crypto');
const { verifyPassword } = require('./store');
const { answerLocal } = require('./jarvis-local');
const { replyLanguage, chatImages, voiceWav, validateWav } = require('./chat-media');
const {readVoiceUpload, voiceError} = require('./voice-upload');
const { presence } = require('./presence');

const COOKIE = 'jarvis_oms_session';
const attempts = new Map();
const fail = (status, message) => { const error=new Error(message); error.status=status; throw error; };
const wrap = (fn) => (req,res,next) => Promise.resolve().then(()=>fn(req,res)).catch(next);
const bounded = (value,max=100) => typeof value==='string' ? value.trim().slice(0,max) : '';
const publicUser = (user) => ({
  id:user.id, username:user.username, displayName:user.display_name, role:user.role,
  employeeId:user.employee_id, language:user.language, canEdit:Number(user.is_admin)===1
});
const mobilePublicUser = (user) => ({
  id:user.id,employeeCode:user.username,role:user.role,language:user.language,employeeId:user.employee_id
});
function cookieValue(req) {
  const match=(req.headers.cookie||'').split(';').map(x=>x.trim()).find(x=>x.startsWith(COOKIE+'='));
  return match ? match.slice(COOKIE.length+1) : '';
}
function sameOrigin(req) {
  const origin=req.get('Origin');
  if (!origin) return;
  let parsed;
  try { parsed=new URL(origin); } catch (_) { fail(403,'Invalid request origin'); }
  if (parsed.origin!==`${req.protocol}://${req.get('Host')}`) fail(403,'Invalid request origin');
}
function requireBodyKeys(body,allowed) {
  if (!body || typeof body!=='object' || Array.isArray(body) || Object.keys(body).some(key=>!allowed.includes(key))) fail(422,'Invalid request fields');
}
function createMobileRouter(oms,knowledge,ai={}) {
  const mobile=express.Router();
  // Server-owned history: one authenticated session cannot supply another user's messages.
  const chats=new Map(), usage=new Map();
  function consumeBudget(userId) {
    const now=Date.now(),day=new Date(now).toISOString().slice(0,10);
    for(const [key,value] of usage)if(value.day!==day)usage.delete(key);
    let budget=usage.get(userId);
    if(!budget){budget={day,total:0,recent:[]};usage.set(userId,budget);}
    budget.recent=budget.recent.filter(time=>now-time<60000);
    if(budget.total>=(ai.daily||100)||budget.recent.length>=(ai.rpm||20))fail(429,'Đã đạt giới hạn AI. Vui lòng thử lại sau.');
    budget.total++;budget.recent.push(now);
  }
  mobile.use((req,res,next)=>{
    if(req.secure||['localhost','127.0.0.1','::1'].includes(req.hostname)) return next();
    next(Object.assign(new Error('Mobile API requires HTTPS outside local development'),{status:403}));
  });
  mobile.post('/login',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['employeeCode','password']);
    const employeeCode=bounded(req.body.employeeCode,40);
    const password=typeof req.body.password==='string'?req.body.password:'';
    if(!(/^NV-\d{5}$/.test(employeeCode)||/^admin$/i.test(employeeCode))||!password||password.length>256) fail(422,'Enter employee code or admin and password');
    const key=`mobile:${req.ip}:${employeeCode}`;
    const record=attempts.get(key);
    if(record&&record.until>Date.now()&&record.count>=5) fail(429,'Too many attempts; retry later');
    const user=await oms.userByUsername(employeeCode);
    const demoDisabled=process.env.NODE_ENV==='production'&&process.env.OMS_SEED_DEMO!=='true'&&employeeCode==='NV-00101';
    const isAdmin=user?.role==='MANAGER'&&Number(user.is_admin)===1;
    if(!user||(!isAdmin&&user.role!=='EMPLOYEE')||demoDisabled||!verifyPassword(password,user.password_hash)){
      const count=record&&record.until>Date.now()?record.count:0;
      attempts.set(key,{count:count+1,until:Date.now()+15*60*1000});
      fail(401,'Invalid employee code or password');
    }
    attempts.delete(key);
    const session=await oms.createMobileSession(user.id);
    res.json({user:mobilePublicUser(user),accessToken:session.token,expiresAt:session.expiresAt});
  }));
  mobile.use((req,res,next)=>Promise.resolve().then(async()=>{
    const auth=req.get('Authorization')||'';
    if(!auth.startsWith('Bearer ')||auth.length>256) fail(401,'Employee session required');
    req.mobileToken=auth.slice(7);
    req.mobileUser=await oms.mobileSessionByToken(req.mobileToken);
    const isAdmin=req.mobileUser?.role==='MANAGER'&&Number(req.mobileUser.is_admin)===1;
    if(!req.mobileUser||(!isAdmin&&(req.mobileUser.role!=='EMPLOYEE'||!req.mobileUser.employee_id))) fail(401,'Employee session expired');
    if(isAdmin&&!['/me','/logout','/presence','/presence/heartbeat','/account/password','/jarvis/chat','/jarvis/transcribe','/jarvis/speech','/jarvis/voice'].includes(req.path))fail(403,'Admin quản lý kho trên web. Phiên app admin chỉ dùng JarVis AI.');
  }).then(()=>next()).catch(next));
  mobile.get('/me',wrap(async(req,res)=>res.json({user:mobilePublicUser(req.mobileUser)})));
  mobile.get('/presence',wrap(async(req,res)=>res.json(await presence(oms,req.mobileUser.id))));
  mobile.post('/presence',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['status']);
    if(!['ONLINE','OFFLINE'].includes(req.body.status))fail(422,'Choose ONLINE or OFFLINE');
    res.json(await presence(oms,req.mobileUser.id,req.body.status));
  }));
  mobile.post('/presence/heartbeat',wrap(async(req,res)=>{
    requireBodyKeys(req.body,[]);
    res.json(await presence(oms,req.mobileUser.id,'HEARTBEAT'));
  }));
  mobile.post('/logout',wrap(async(req,res)=>{await presence(oms,req.mobileUser.id,'OFFLINE');await oms.deleteMobileSession(req.mobileToken);res.json({ok:true});}));
  mobile.post('/account/password',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['currentPassword','newPassword']);
    const {currentPassword,newPassword}=req.body;
    if(typeof currentPassword!=='string'||!currentPassword||currentPassword.length>256||typeof newPassword!=='string'||newPassword.length<12||newPassword.length>256)
      fail(422,'New password must contain 12 to 256 characters');
    const key='password:'+req.mobileUser.id,record=attempts.get(key);
    if(record&&record.until>Date.now()&&record.count>=5)fail(429,'Too many attempts; retry later');
    try {await oms.changeOwnPassword(req.mobileUser.id,currentPassword,newPassword);}
    catch(error){const count=record&&record.until>Date.now()?record.count:0;attempts.set(key,{count:count+1,until:Date.now()+15*60*1000});throw error;}
    attempts.delete(key);res.json({ok:true,loginRequired:true});
  }));
  mobile.get('/tasks',wrap(async(req,res)=>res.json({tasks:await oms.tasks(req.mobileUser.employee_id),synthetic:oms.synthetic})));
  mobile.get('/inventory',wrap(async(req,res)=>res.json({items:await oms.inventory(''),synthetic:oms.synthetic})));
  mobile.get('/inventory/search',wrap(async(req,res)=>{
    const query=bounded(req.query.q,120);
    if(!query) fail(422,'Enter a barcode, SKU, lot or location');
    res.json({items:await oms.inventory(query),synthetic:oms.synthetic});
  }));
  mobile.post('/tasks/:id/status',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['status','requestId']);
    const allowed=['ACCEPTED','IN_PROGRESS','BLOCKED','COMPLETED'];
    if(!allowed.includes(req.body.status)||typeof req.body.requestId!=='string'||!/^[0-9a-f-]{36}$/i.test(req.body.requestId)) fail(422,'Invalid task event');
    const task=await oms.task(req.params.id);
    if(!task) fail(404,'Task not found');
    if(task.employee_id!==req.mobileUser.employee_id) fail(403,'Task is not assigned to this employee');
    const value=await oms.taskTransition(task.id,req.body.status,req.mobileUser.id,req.body.requestId);
    res.json(value);
  }));
  mobile.get('/conversations',wrap(async(req,res)=>res.json({conversations:await oms.conversations(req.mobileUser.employee_id)})));
  mobile.get('/conversations/:id/messages',wrap(async(req,res)=>{
    const conversation=await oms.conversation(req.params.id);
    if(!conversation||conversation.employeeId!==req.mobileUser.employee_id) fail(404,'Conversation not found');
    res.json({messages:await oms.messages(conversation.id)});
  }));
  mobile.post('/conversations/:id/messages',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['content']);
    const conversation=await oms.conversation(req.params.id);
    if(!conversation||conversation.employeeId!==req.mobileUser.employee_id) fail(404,'Conversation not found');
    const content=bounded(req.body.content,2000);
    if(!content) fail(422,'Message is empty');
    const message=await oms.addMessage(conversation.id,req.mobileUser.id,content);
    res.status(201).json(message);
  }));
  function reserveTurn(req) {
    const now=Date.now();
    for(const [key,value] of chats) if(now-value.updated>3600000&&!value.busy) chats.delete(key);
    const key=crypto.createHash('sha256').update(req.mobileToken).digest('hex');
    let state=chats.get(key);
    if(!state){if(chats.size>=2000)fail(503,'AI đang bận. Vui lòng thử lại sau.');state={messages:[],updated:now,busy:false,language:req.mobileUser.language};chats.set(key,state);}
    if(state.busy)fail(409,'JarVis đang trả lời. Vui lòng chờ.');
    consumeBudget(req.mobileUser.id);state.busy=true;state.updated=now;
    return state;
  }
  async function chatTurn(req,state,message,images=[],signal) {
      if(signal?.aborted)throw voiceError('TIMEOUT','Voice request cancelled',504);
      const local=await answerLocal({message,user:req.mobileUser,oms,knowledge});
      const language=replyLanguage(message,state.language);
      const answer=await ai.answer({message,user:req.mobileUser,history:state.messages.slice(),local,images,language,signal});
      if(signal?.aborted)throw voiceError('TIMEOUT','Voice request cancelled',504);
      if(!answer||typeof answer.message!=='string'||!answer.message.trim())fail(502,'AI chưa trả về câu trả lời hợp lệ.');
      // Retain only a text marker in history; photo bytes are not persisted or replayed on follow-ups.
      state.messages.push({role:'user',content:message+(images.length?'\n[Photo attached to this turn; image is not retained]':'')},{role:'assistant',content:answer.message});
      state.language=language;
      state.messages=state.messages.slice(-12);state.updated=Date.now();
      return {...answer,language,mode:'openai',synthetic:oms.synthetic,operationalMutation:false};
  }
  mobile.post('/jarvis/chat',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['message','images']);
    const images=chatImages(req.body.images);
    let message=bounded(req.body.message,1000);
    if(!message&&!images.length) fail(422,'Enter a question or attach a photo');
    if(!message)message='Hãy mô tả ảnh này và hỗ trợ tôi trong công việc ở kho.';
    if(!ai.answer){if(images.length)fail(503,'OpenAI image chat is not configured');return res.json({...await answerLocal({message,user:req.mobileUser,oms,knowledge}),mode:'local-data',synthetic:oms.synthetic,operationalMutation:false});}
    const state=reserveTurn(req);
    try{res.json(await chatTurn(req,state,message,images));
    }finally{state.busy=false;}
  }));
  mobile.post('/jarvis/voice',wrap(async(req,res)=>{
    if(!ai.transcribe||!ai.answer)throw voiceError('OPENAI_CONFIGURATION_ERROR','OpenAI voice chat is not configured',503);
    // Reserve before upload/transcription, preventing concurrent voice/text turns and duplicate cost.
    const state=reserveTurn(req),controller=new AbortController();
    const cancel=()=>{if(!res.writableEnded)controller.abort();};
    const timer=setTimeout(()=>controller.abort(),95000);res.on('close',cancel);
    let stage='upload';
    try {
      const bytes=await readVoiceUpload(req,controller.signal);
      try{validateWav(bytes,true);}catch(error){throw voiceError(error.code||'INVALID_AUDIO',error.message);}
      stage='transcription';
      const result=await ai.transcribe(bytes,controller.signal);
      if(controller.signal.aborted)throw voiceError('TIMEOUT','Voice request timed out',504);
      const transcript=typeof result?.text==='string'?result.text.trim():'';
      if(!transcript)throw voiceError('EMPTY_TRANSCRIPT','No usable speech detected');
      if(transcript.length>1000)throw voiceError('INVALID_AUDIO','Record a shorter question');
      stage='chat';
      const reply=await chatTurn(req,state,transcript,[],controller.signal);
      res.json({success:true,transcript,reply:reply.message,...reply});
    } catch(error) {
      if(!error.code)error=voiceError(error.status===504?'TIMEOUT':stage==='transcription'?'TRANSCRIPTION_FAILED':stage==='chat'?'AI_REQUEST_FAILED':'SERVER_ERROR',
        'Voice request failed. Please try again.',error.status||500);
      throw error;
    } finally {clearTimeout(timer);res.removeListener('close',cancel);controller.abort();state.busy=false;}
  }));
  mobile.post('/jarvis/transcribe',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['audio']);
    const audio=voiceWav(req.body.audio);
    if(!ai.transcribe)fail(503,'OpenAI voice input is not configured');
    consumeBudget(req.mobileUser.id);
    res.json(await ai.transcribe(audio));
  }));
  mobile.post('/jarvis/speech',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['text']);
    if(typeof req.body.text!=='string'||!req.body.text.trim()||req.body.text.length>4000)fail(422,'Read at most 4000 characters per answer');
    if(!ai.speak)fail(503,'OpenAI voice output is not configured');
    consumeBudget(req.mobileUser.id);
    const audio=await ai.speak(req.body.text.trim());
    res.type('audio/mpeg').send(audio);
  }));
  mobile.use((error,req,res,next)=>{
    if(res.headersSent) return next(error);
    const status=Number.isInteger(error.status)?error.status:500;
    const voice=req.path==='/jarvis/voice';
    res.status(status).json({...(voice?{success:false,code:error.code||(status===401?'AUTH_REQUIRED':status===409?'BUSY':status===429?'RATE_LIMIT':'SERVER_ERROR')}:{ }),detail:status===500?'JarVis mobile service error':error.message});
  });
  return mobile;
}
function createOmsRouter(oms,knowledge,ai={}) {
  const router=express.Router();
  router.use('/mobile',createMobileRouter(oms,knowledge,ai));
  router.post('/auth/login',wrap(async(req,res)=>{
    sameOrigin(req);
    requireBodyKeys(req.body,['username','password']);
    const username=bounded(req.body.username,80);
    const password=typeof req.body.password==='string'?req.body.password:'';
    const key=`${req.ip}:${username.toLowerCase()}`;
  const record=attempts.get(key);
    if (record && record.until>Date.now() && record.count>=5) fail(429,'Too many attempts; retry later');
    if (!username || !password || password.length>256) fail(422,'Enter username and password');
    const user=await oms.userByUsername(username);
    const demoDisabled=process.env.NODE_ENV==='production' && process.env.OMS_SEED_DEMO!=='true' && ['JustinandJarVis','NV-00101'].includes(username);
    if (!user || demoDisabled || !verifyPassword(password,user.password_hash)) {
      const old=record && record.until>Date.now()?record.count:0;
      attempts.set(key,{ count:old+1,until:Date.now()+15*60*1000 });
      fail(401,'Invalid username or password');
    }
    attempts.delete(key);
    const session=await oms.createSession(user.id);
    res.cookie(COOKIE,session.token,{ httpOnly:true,sameSite:'strict',secure:process.env.NODE_ENV==='production',path:'/api/oms',maxAge:8*60*60*1000 });
    res.json({ user:publicUser(user),csrfToken:session.csrf,expiresAt:session.expires,dataMode:oms.synthetic?'sample':'live' });
  }));

  router.use((req,res,next)=>Promise.resolve().then(async()=>{
    const token=cookieValue(req);
    const session=await oms.sessionByToken(token);
    if (!session) fail(401,'Sign in to JarVis Warehouse');
    req.omsToken=token;
    req.omsUser=session;
    if (!['GET','HEAD','OPTIONS'].includes(req.method)) {
      sameOrigin(req);
      if (req.get('X-CSRF-Token')!==session.csrf_token) fail(403,'Invalid CSRF token');
    }
  }).then(()=>next()).catch(next));
  router.use((req,res,next)=>{
    if (['GET','HEAD','OPTIONS'].includes(req.method) ||
        ['/auth/logout','/settings','/jarvis/chat'].includes(req.path) ||
        /^\/conversations\/[^/]+\/messages$/.test(req.path)) return next();
    if (Number(req.omsUser.is_admin)!==1) return next(Object.assign(new Error('Admin permission required to change warehouse data'),{status:403}));
    next();
  });
  const manager = (req) => {
    if (req.omsUser.role!=='MANAGER') fail(403,'Manager permission required');
  };

  router.get('/auth/me',wrap(async(req,res)=>res.json({user:publicUser(req.omsUser),csrfToken:req.omsUser.csrf_token,dataMode:oms.synthetic?'sample':'live'})));
  router.get('/accounts',wrap(async(req,res)=>{
    manager(req);
    if(Number(req.omsUser.is_admin)!==1) fail(403,'Admin permission required');
    res.json(await oms.accounts());
  }));
  router.post('/accounts',wrap(async(req,res)=>{
    manager(req);
    if(Number(req.omsUser.is_admin)!==1) fail(403,'Admin permission required');
    requireBodyKeys(req.body,['username','password','displayName','role','workRole','warehouseZone']);
    const role=bounded(req.body.role,20);
    const username=bounded(req.body.username,80);
    const displayName=bounded(req.body.displayName,120);
    const password=req.body.password;
    const workRole=bounded(req.body.workRole,40);
    const warehouseZone=bounded(req.body.warehouseZone,30);
    if(!['MANAGER','EMPLOYEE'].includes(role)||!displayName||typeof password!=='string'||password.length<12||password.length>256) fail(422,'Account fields or password are invalid');
    if(role==='EMPLOYEE' && (!/^NV-\d{5}$/.test(username)||!['RECEIVING','PUTAWAY','PICKING','PACKING','COUNTING','EXPIRY_CHECK'].includes(workRole))) fail(422,'Employee code or work role is invalid');
    if(role==='MANAGER' && (!/^[A-Za-z0-9._-]{3,80}$/.test(username)||workRole||warehouseZone)) fail(422,'Viewer account fields are invalid');
    if(warehouseZone && !/^[A-Za-z0-9-]{1,30}$/.test(warehouseZone)) fail(422,'Warehouse zone is invalid');
    res.status(201).json(await oms.createAccount({role,username,displayName,password,workRole,warehouseZone},req.omsUser.id));
  }));
  router.post('/auth/logout',wrap(async(req,res)=>{
    await oms.deleteSession(req.omsToken);
    res.clearCookie(COOKIE,{path:'/api/oms',sameSite:'strict',secure:process.env.NODE_ENV==='production'});
    res.json({ok:true});
  }));
  router.get('/dashboard',wrap(async(req,res)=>{ manager(req); res.json({...await oms.dashboard(),jarvisMode:'local-data'}); }));
  router.get('/products',wrap(async(req,res)=>res.json(await oms.products(bounded(req.query.q,120)))));
  router.post('/products',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['sku','name','barcode','unit','lotTrackingRequired']);
    const sku=bounded(req.body.sku,64);
    const name=bounded(req.body.name,180);
    const barcode=bounded(req.body.barcode,64);
    const unit=bounded(req.body.unit,24);
    if(!/^[A-Za-z0-9._-]{3,64}$/.test(sku)||!name||!(/^[A-Za-z0-9._-]{4,64}$/.test(barcode))||!unit) fail(422,'Product SKU, name, barcode or unit is invalid');
    const value=await oms.createProduct({sku,name,barcode,unit,lotTrackingRequired:req.body.lotTrackingRequired!==false},req.omsUser.id);
    res.status(201).json(value);
  }));
  router.get('/locations',wrap(async(req,res)=>res.json(await oms.locations())));
  router.post('/locations',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['code','zone']);
    const code=bounded(req.body.code,40);
    const zone=bounded(req.body.zone,30);
    if(!/^[A-Za-z0-9-]{2,40}$/.test(code)||!zone) fail(422,'Location code or zone is invalid');
    res.status(201).json(await oms.createLocation({code,zone},req.omsUser.id));
  }));
  router.get('/lots',wrap(async(req,res)=>res.json(await oms.lots(bounded(req.query.productId,80)||null))));
  router.post('/lots',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['productId','lotCode','receivedAt','expiryDate']);
    const productId=bounded(req.body.productId,80);
    const lotCode=bounded(req.body.lotCode,64);
    const receivedAt=bounded(req.body.receivedAt,10);
    const expiryDate=bounded(req.body.expiryDate,10);
    if(!productId||!/^[A-Za-z0-9._-]{2,64}$/.test(lotCode)||!/^\d{4}-\d{2}-\d{2}$/.test(receivedAt)||expiryDate&&!/^\d{4}-\d{2}-\d{2}$/.test(expiryDate)) fail(422,'Lot input is invalid');
    const value=await oms.createLot({productId,lotCode,receivedAt,expiryDate},req.omsUser.id);
    if(!value) fail(404,'Product not found');
    res.status(201).json(value);
  }));
  router.get('/orders',wrap(async(req,res)=>{ manager(req); res.json(await oms.orders(bounded(req.query.q,120))); }));
  router.get('/orders/:code',wrap(async(req,res)=>{
    manager(req);
    const order=await oms.order(req.params.code);
    if (!order) fail(404,'Order not found');
    res.json(order);
  }));
  router.get('/returns',wrap(async(req,res)=>{ manager(req); res.json(await oms.returns(bounded(req.query.q,120))); }));
  router.post('/returns',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['orderId','orderItemId','quantity','reason','notes']);
    if(!req.body.orderId||!req.body.orderItemId||!Number.isInteger(req.body.quantity)||req.body.quantity<1||!bounded(req.body.reason,200)) fail(422,'Return input is invalid');
    const value=await oms.createReturn({orderId:req.body.orderId,orderItemId:req.body.orderItemId,quantity:req.body.quantity,reason:bounded(req.body.reason,200),notes:bounded(req.body.notes,500)},req.omsUser.id);
    if(!value) fail(422,'Order item does not match this order or quantity');
    res.status(201).json(value);
  }));
  router.get('/returns/:code',wrap(async(req,res)=>{
    manager(req);
    const value=await oms.returnDetail(req.params.code);
    if (!value) fail(404,'Return not found');
    res.json(value);
  }));
  router.post('/returns/:code/status',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['status']);
    if(!['IN_TRANSIT','RECEIVED','INSPECTING'].includes(req.body.status)) fail(422,'Invalid return status');
    const value=await oms.advanceReturn(req.params.code,req.body.status,req.omsUser.id);
    if(!value) fail(404,'Return not found');
    res.json(value);
  }));
  router.post('/returns/:code/restock',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['returnItemId','lotId','locationId','quantity','requestId']);
    if(!req.body.returnItemId||!req.body.lotId||!req.body.locationId||!Number.isInteger(req.body.quantity)||req.body.quantity<1||typeof req.body.requestId!=='string'||!/^[-0-9a-f]{36}$/i.test(req.body.requestId)) fail(422,'Restock input is invalid');
    const value=await oms.restockReturn({returnId:req.params.code,returnItemId:req.body.returnItemId,lotId:req.body.lotId,locationId:req.body.locationId,quantity:req.body.quantity,requestId:req.body.requestId},req.omsUser.id);
    if(!value) fail(404,'Return not found');
    res.json(value);
  }));
  router.get('/inventory/search',wrap(async(req,res)=>res.json(await oms.inventory(bounded(req.query.q,120)))));
  router.get('/inventory/suggest/:productId',wrap(async(req,res)=>res.json(await oms.pickSuggestion(req.params.productId))));
  router.get('/receiving',wrap(async(req,res)=>{ manager(req); res.json(await oms.receivings()); }));
  router.post('/receiving',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['productId','lotId','locationId','expectedQuantity']);
    if(!req.body.productId||!req.body.lotId||!req.body.locationId||!Number.isInteger(req.body.expectedQuantity)||req.body.expectedQuantity<0) fail(422,'Receiving input is invalid');
    const value=await oms.createReceiving(req.body,req.omsUser.id);
    if(!value) fail(422,'Product, lot or location does not match');
    res.status(201).json(value);
  }));
  router.post('/receiving/:id/confirm',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['actualQuantity','requestId']);
    if (!Number.isInteger(req.body.actualQuantity) || req.body.actualQuantity<0 || req.body.actualQuantity>100000) fail(422,'Invalid actual quantity');
    if (typeof req.body.requestId!=='string' || !/^[0-9a-f-]{36}$/i.test(req.body.requestId)) fail(422,'Invalid request ID');
    const value=await oms.confirmReceiving(req.params.id,req.body.actualQuantity,req.body.requestId,req.omsUser.id);
    if (!value) fail(404,'Receiving not found');
    res.json(value);
  }));
  router.get('/tasks',wrap(async(req,res)=>res.json(await oms.tasks(req.omsUser.role==='EMPLOYEE'?req.omsUser.employee_id:null))));
  router.post('/tasks',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['taskType','title','priority','sourceType','sourceId','productId','locationId','expectedQuantity','dueAt']);
    const type=bounded(req.body.taskType,40);
    const title=bounded(req.body.title,180);
    if (!['RECEIVING','PUTAWAY','PICKING','PACKING','COUNTING','EXPIRY_CHECK'].includes(type) || !title) fail(422,'Invalid task type or title');
    if (req.body.expectedQuantity!==undefined && (!Number.isInteger(req.body.expectedQuantity) || req.body.expectedQuantity<0)) fail(422,'Invalid expected quantity');
    if (req.body.priority!==undefined && !['LOW','NORMAL','HIGH'].includes(req.body.priority)) fail(422,'Invalid task priority');
    if (req.body.dueAt!==undefined && req.body.dueAt!==null && (typeof req.body.dueAt!=='string'||Number.isNaN(Date.parse(req.body.dueAt)))) fail(422,'Invalid due date');
    const task=await oms.createTask({ ...req.body,taskType:type,title },req.omsUser.id);
    res.status(201).json(task);
  }));
  router.get('/tasks/:id/suggestion',wrap(async(req,res)=>{
    manager(req);
    const suggestion=await oms.suggestEmployee(req.params.id);
    if (!suggestion) fail(404,'No ONLINE employee is available for this task');
    res.json(suggestion);
  }));
  router.post('/tasks/:id/assign',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['employeeCode']);
    const code=bounded(req.body.employeeCode,40);
    if (!/^NV-\d{5}$/.test(code)) fail(422,'Invalid employee code');
    const task=await oms.assignTask(req.params.id,code,req.omsUser.id);
    if (!task) fail(404,'Task not found');
    res.json(task);
  }));
  router.post('/tasks/:id/status',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['status']);
    const task=await oms.task(req.params.id);
    if (!task) fail(404,'Task not found');
    if (req.omsUser.role==='EMPLOYEE' && task.employee_id!==req.omsUser.employee_id) fail(403,'Task is not assigned to this employee');
    if (Number(req.omsUser.is_admin)!==1) fail(403,'Admin permission required');
    const updated=await oms.taskTransition(task.id,req.body.status,req.omsUser.id);
    res.json(updated);
  }));
  router.get('/employees',wrap(async(req,res)=>{ manager(req); res.json(await oms.employees()); }));
  router.get('/conversations',wrap(async(req,res)=>{
    if (!['MANAGER','EMPLOYEE'].includes(req.omsUser.role)) fail(403,'Chat access denied');
    res.json(await oms.conversations(req.omsUser.role==='EMPLOYEE'?req.omsUser.employee_id:null));
  }));
  router.post('/conversations',wrap(async(req,res)=>{
    manager(req);
    requireBodyKeys(req.body,['employeeCode']);
    const code=bounded(req.body.employeeCode,40);
    if (!/^NV-\d{5}$/.test(code)) fail(422,'Invalid employee code');
    const conversation=await oms.ensureConversation(code);
    if (!conversation) fail(404,'Employee not found');
    res.json(conversation);
  }));
  router.get('/conversations/:id/messages',wrap(async(req,res)=>{
    const conversation=await oms.conversation(req.params.id);
    if (!conversation) fail(404,'Conversation not found');
    if (req.omsUser.role==='EMPLOYEE' && conversation.employeeId!==req.omsUser.employee_id) fail(403,'Conversation access denied');
    if (!['MANAGER','EMPLOYEE'].includes(req.omsUser.role)) fail(403,'Conversation access denied');
    res.json(await oms.messages(req.params.id));
  }));
  router.post('/conversations/:id/messages',wrap(async(req,res)=>{
    if(Number(req.omsUser.is_admin)!==1) fail(403,'Admin permission required to send web messages');
    const conversation=await oms.conversation(req.params.id);
    if (!conversation) fail(404,'Conversation not found');
    if (req.omsUser.role==='EMPLOYEE' && conversation.employeeId!==req.omsUser.employee_id) fail(403,'Conversation access denied');
    if (!['MANAGER','EMPLOYEE'].includes(req.omsUser.role)) fail(403,'Conversation access denied');
    requireBodyKeys(req.body,['content']);
    const content=bounded(req.body.content,2000);
    if (!content) fail(422,'Message is empty');
    const message=await oms.addMessage(req.params.id,req.omsUser.id,content);
    if (!message) fail(404,'Conversation not found');
    res.status(201).json(message);
  }));
  router.get('/settings',wrap(async(req,res)=>res.json({ language:req.omsUser.language,user:publicUser(req.omsUser) })));
  router.patch('/settings',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['language']);
    if (!['vi','en'].includes(req.body.language)) fail(422,'Choose Vietnamese or English');
    await oms.setLanguage(req.omsUser.id,req.body.language);
    res.json({language:req.body.language});
  }));
  router.get('/jarvis/conversations',wrap(async(req,res)=>res.json(await oms.aiConversations(req.omsUser.id))));
  router.get('/jarvis/conversations/:id/messages',wrap(async(req,res)=>{
    const conversation=await oms.aiConversation(req.params.id,req.omsUser.id);
    if(!conversation) fail(404,'JarVis conversation not found');
    res.json(await oms.aiMessages(conversation.id));
  }));
  router.post('/jarvis/chat',wrap(async(req,res)=>{
    requireBodyKeys(req.body,['message','conversationId']);
    const message=bounded(req.body.message,1000);
    if(!message) fail(422,'Enter a question');
    let conversationId=req.body.conversationId;
    if(conversationId){
      const conversation=await oms.aiConversation(conversationId,req.omsUser.id);
      if(!conversation) fail(404,'JarVis conversation not found');
    }else{
      conversationId=await oms.createAiConversation(req.omsUser.id,message);
    }
    await oms.addAiMessage(conversationId,'user',message);
    const answer=await answerLocal({message,user:req.omsUser,oms,knowledge});
    await oms.addAiMessage(conversationId,'assistant',answer.message,answer.sources);
    res.json({...answer,conversationId,mode:'local-data',synthetic:oms.synthetic,operationalMutation:false,externalTransmission:false});
  }));
  router.use((error,req,res,next)=>{
    if (res.headersSent) return next(error);
    const status=error.code?.startsWith('SQLITE_CONSTRAINT')?409:Number.isInteger(error.status)&&error.status>=400&&error.status<500?error.status:500;
    res.status(status).json({detail:status===500?'JarVis warehouse service error':status===409&&error.code?.startsWith('SQLITE_CONSTRAINT')?'Record already exists or violates an inventory rule':error.message});
  });
  return router;
}

module.exports = { createOmsRouter };
