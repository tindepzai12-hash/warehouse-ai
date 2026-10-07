PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS oms_users (
  id TEXT PRIMARY KEY,
  username TEXT NOT NULL UNIQUE COLLATE NOCASE,
  password_hash TEXT NOT NULL,
  display_name TEXT NOT NULL,
  role TEXT NOT NULL CHECK(role IN ('MANAGER','EMPLOYEE','MERCHANT','FINANCE')),
  is_admin INTEGER NOT NULL DEFAULT 0 CHECK(is_admin IN (0,1)),
  employee_id TEXT,
  language TEXT NOT NULL DEFAULT 'vi' CHECK(language IN ('vi','en')),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS oms_sessions (
  token_hash TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES oms_users(id) ON DELETE CASCADE,
  csrf_token TEXT NOT NULL,
  expires_at TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS oms_sessions_expiry ON oms_sessions(expires_at);
CREATE TABLE IF NOT EXISTS oms_mobile_sessions (
  token_hash TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES oms_users(id) ON DELETE CASCADE,
  expires_at TEXT NOT NULL,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS oms_mobile_sessions_expiry ON oms_mobile_sessions(expires_at);

CREATE TABLE IF NOT EXISTS oms_online_sessions (
  id TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES oms_users(id) ON DELETE CASCADE,
  online_started_at TEXT NOT NULL,
  online_ended_at TEXT,
  lease_until TEXT NOT NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS oms_one_online_session ON oms_online_sessions(user_id) WHERE online_ended_at IS NULL;
CREATE INDEX IF NOT EXISTS oms_online_history ON oms_online_sessions(user_id,online_started_at);

CREATE TABLE IF NOT EXISTS oms_employees (
  id TEXT PRIMARY KEY,
  employee_code TEXT NOT NULL UNIQUE,
  user_id TEXT UNIQUE REFERENCES oms_users(id),
  name TEXT,
  role TEXT NOT NULL,
  status TEXT NOT NULL DEFAULT 'OFFLINE' CHECK(status IN ('ONLINE','OFFLINE')),
  warehouse_zone TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS oms_products (
  id TEXT PRIMARY KEY,
  sku TEXT NOT NULL UNIQUE COLLATE NOCASE,
  name TEXT NOT NULL,
  barcode TEXT NOT NULL UNIQUE,
  unit TEXT NOT NULL,
  lot_tracking_required INTEGER NOT NULL DEFAULT 1 CHECK(lot_tracking_required IN (0,1)),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS oms_products_name ON oms_products(name);
CREATE TABLE IF NOT EXISTS oms_locations (
  id TEXT PRIMARY KEY,
  code TEXT NOT NULL UNIQUE COLLATE NOCASE,
  zone TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS oms_lots (
  id TEXT PRIMARY KEY,
  product_id TEXT NOT NULL REFERENCES oms_products(id),
  lot_code TEXT NOT NULL,
  manufacture_date TEXT,
  expiry_date TEXT,
  received_at TEXT NOT NULL,
  UNIQUE(product_id,lot_code)
);
CREATE INDEX IF NOT EXISTS oms_lots_code ON oms_lots(lot_code);
CREATE TABLE IF NOT EXISTS oms_balances (
  id TEXT PRIMARY KEY,
  product_id TEXT NOT NULL REFERENCES oms_products(id),
  lot_id TEXT NOT NULL REFERENCES oms_lots(id),
  location_id TEXT NOT NULL REFERENCES oms_locations(id),
  quantity_on_hand INTEGER NOT NULL DEFAULT 0 CHECK(quantity_on_hand >= 0),
  quantity_reserved INTEGER NOT NULL DEFAULT 0 CHECK(quantity_reserved >= 0),
  quantity_damaged INTEGER NOT NULL DEFAULT 0 CHECK(quantity_damaged >= 0),
  updated_at TEXT NOT NULL,
  CHECK(quantity_on_hand >= quantity_reserved + quantity_damaged),
  UNIQUE(product_id,lot_id,location_id)
);
CREATE TABLE IF NOT EXISTS oms_inventory_movements (
  id TEXT PRIMARY KEY,
  request_id TEXT NOT NULL UNIQUE,
  product_id TEXT NOT NULL REFERENCES oms_products(id),
  lot_id TEXT NOT NULL REFERENCES oms_lots(id),
  location_id TEXT NOT NULL REFERENCES oms_locations(id),
  movement_type TEXT NOT NULL,
  source_type TEXT NOT NULL,
  source_id TEXT NOT NULL,
  quantity_change INTEGER NOT NULL,
  quantity_before INTEGER NOT NULL,
  quantity_after INTEGER NOT NULL,
  actor_user_id TEXT NOT NULL REFERENCES oms_users(id),
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS oms_movements_source ON oms_inventory_movements(source_type,source_id);

CREATE TABLE IF NOT EXISTS oms_orders (
  id TEXT PRIMARY KEY,
  order_code TEXT NOT NULL UNIQUE,
  customer_reference TEXT,
  status TEXT NOT NULL CHECK(status IN ('NEW','ALLOCATED','PICKING','PACKING','SHIPPED','CANCELLED')),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS oms_orders_status ON oms_orders(status,created_at);
CREATE TABLE IF NOT EXISTS oms_order_items (
  id TEXT PRIMARY KEY,
  order_id TEXT NOT NULL REFERENCES oms_orders(id),
  product_id TEXT NOT NULL REFERENCES oms_products(id),
  quantity INTEGER NOT NULL CHECK(quantity > 0)
);
CREATE TABLE IF NOT EXISTS oms_returns (
  id TEXT PRIMARY KEY,
  return_code TEXT NOT NULL UNIQUE,
  order_id TEXT NOT NULL REFERENCES oms_orders(id),
  status TEXT NOT NULL CHECK(status IN ('REQUESTED','IN_TRANSIT','RECEIVED','INSPECTING','RESTOCKED','CLOSED')),
  reason TEXT NOT NULL,
  created_at TEXT NOT NULL,
  received_at TEXT,
  employee_id TEXT REFERENCES oms_employees(id),
  notes TEXT
);
CREATE INDEX IF NOT EXISTS oms_returns_status ON oms_returns(status,created_at);
CREATE TABLE IF NOT EXISTS oms_return_items (
  id TEXT PRIMARY KEY,
  return_id TEXT NOT NULL REFERENCES oms_returns(id),
  order_item_id TEXT NOT NULL REFERENCES oms_order_items(id),
  product_id TEXT NOT NULL REFERENCES oms_products(id),
  lot_id TEXT REFERENCES oms_lots(id),
  quantity INTEGER NOT NULL CHECK(quantity > 0),
  condition TEXT NOT NULL DEFAULT 'UNINSPECTED',
  disposition TEXT
);
CREATE TABLE IF NOT EXISTS oms_receivings (
  id TEXT PRIMARY KEY,
  receiving_code TEXT NOT NULL UNIQUE,
  product_id TEXT NOT NULL REFERENCES oms_products(id),
  lot_id TEXT NOT NULL REFERENCES oms_lots(id),
  location_id TEXT NOT NULL REFERENCES oms_locations(id),
  expected_quantity INTEGER NOT NULL CHECK(expected_quantity >= 0),
  received_quantity INTEGER NOT NULL DEFAULT 0 CHECK(received_quantity >= 0),
  shortage_quantity INTEGER NOT NULL DEFAULT 0 CHECK(shortage_quantity >= 0),
  status TEXT NOT NULL CHECK(status IN ('PENDING','COMPLETE','SHORTAGE')),
  created_at TEXT NOT NULL,
  completed_at TEXT
);
CREATE INDEX IF NOT EXISTS oms_receivings_status ON oms_receivings(status,created_at);

CREATE TABLE IF NOT EXISTS oms_tasks (
  id TEXT PRIMARY KEY,
  task_code TEXT NOT NULL UNIQUE,
  task_type TEXT NOT NULL,
  title TEXT NOT NULL,
  status TEXT NOT NULL CHECK(status IN ('UNASSIGNED','ASSIGNED','ACCEPTED','IN_PROGRESS','BLOCKED','COMPLETED')),
  priority TEXT NOT NULL DEFAULT 'NORMAL' CHECK(priority IN ('LOW','NORMAL','HIGH')),
  employee_id TEXT REFERENCES oms_employees(id),
  source_type TEXT,
  source_id TEXT,
  product_id TEXT REFERENCES oms_products(id),
  location_id TEXT REFERENCES oms_locations(id),
  expected_quantity INTEGER CHECK(expected_quantity >= 0),
  due_at TEXT,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS oms_tasks_status ON oms_tasks(status,due_at);
CREATE INDEX IF NOT EXISTS oms_tasks_employee ON oms_tasks(employee_id,status);
CREATE TABLE IF NOT EXISTS oms_task_events (
  id TEXT PRIMARY KEY,
  task_id TEXT NOT NULL REFERENCES oms_tasks(id),
  from_status TEXT,
  to_status TEXT NOT NULL,
  actor_user_id TEXT NOT NULL REFERENCES oms_users(id),
  note TEXT,
  created_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS oms_mobile_event_replays (
  user_id TEXT NOT NULL REFERENCES oms_users(id),
  request_id TEXT NOT NULL,
  task_id TEXT NOT NULL REFERENCES oms_tasks(id),
  response_json TEXT NOT NULL,
  PRIMARY KEY(user_id,request_id)
);
CREATE TABLE IF NOT EXISTS oms_conversations (
  id TEXT PRIMARY KEY,
  employee_id TEXT NOT NULL REFERENCES oms_employees(id),
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS oms_chat_messages (
  id TEXT PRIMARY KEY,
  conversation_id TEXT NOT NULL REFERENCES oms_conversations(id),
  sender_user_id TEXT NOT NULL REFERENCES oms_users(id),
  content TEXT NOT NULL CHECK(length(content) BETWEEN 1 AND 2000),
  created_at TEXT NOT NULL,
  read_at TEXT
);
CREATE INDEX IF NOT EXISTS oms_chat_messages_conversation ON oms_chat_messages(conversation_id,created_at);
CREATE TABLE IF NOT EXISTS oms_ai_conversations (
  id TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES oms_users(id),
  title TEXT NOT NULL,
  created_at TEXT NOT NULL,
  updated_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS oms_ai_conversations_user ON oms_ai_conversations(user_id,updated_at);
CREATE TABLE IF NOT EXISTS oms_ai_messages (
  id TEXT PRIMARY KEY,
  conversation_id TEXT NOT NULL REFERENCES oms_ai_conversations(id),
  role TEXT NOT NULL CHECK(role IN ('user','assistant')),
  content TEXT NOT NULL,
  sources_json TEXT,
  created_at TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS oms_ai_messages_conversation ON oms_ai_messages(conversation_id,created_at);
CREATE TABLE IF NOT EXISTS oms_audit_logs (
  id TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES oms_users(id),
  action TEXT NOT NULL,
  entity_type TEXT NOT NULL,
  entity_id TEXT NOT NULL,
  before_json TEXT,
  after_json TEXT,
  originated_from TEXT NOT NULL DEFAULT 'USER',
  created_at TEXT NOT NULL
);
