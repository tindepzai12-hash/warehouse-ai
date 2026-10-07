const crypto = require('node:crypto');
const LEASE_MS = 120000;
const STREAK_MIN_SECONDS = 60;
// Activity recognition, not payroll attendance. Count only server-recorded online time.
function activityStreak(rows, at) {
  const now = Date.parse(at), day = taipeiDay(at), buckets = new Map();
  for (const row of rows) {
    let start = Date.parse(row.online_started_at);
    const end = Math.min(now, Date.parse(row.online_ended_at || row.lease_until));
    if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) continue;
    while (start < end) {
      const part = taipeiDay(new Date(start).toISOString());
      const stop = Math.min(end, part.end);
      const intervals = buckets.get(part.date) || [];
      intervals.push([start, stop]); buckets.set(part.date, intervals); start = stop;
    }
  }
  const valid = new Set();
  for (const [key, intervals] of buckets) {
    intervals.sort((a,b) => a[0]-b[0]); let total=0, start=null, end=null;
    for (const [a,b] of intervals) {
      if (start === null) {start=a;end=b;}
      else if (a <= end) end=Math.max(end,b);
      else {total+=end-start;start=a;end=b;}
    }
    if (start !== null) total+=end-start;
    if (total >= STREAK_MIN_SECONDS*1000) valid.add(key);
  }
  const dateKey = ms => new Date(ms+8*3600000).toISOString().slice(0,10);
  const todayQualified=valid.has(day.date);
  let cursor=todayQualified?day.start:day.start-86400000, current=0;
  while (valid.has(dateKey(cursor))) {current++;cursor-=86400000;}
  const weekday=(new Date(day.start+8*3600000).getUTCDay()+6)%7;
  const monday=day.start-weekday*86400000;
  return {currentStreak:current,todayQualified,minimumSeconds:STREAK_MIN_SECONDS,
    basis:'ONLINE_ACTIVITY',scheduleAware:false,
    week:Array.from({length:7},(_,i)=>({date:dateKey(monday+i*86400000),qualified:valid.has(dateKey(monday+i*86400000))}))};
}
// Taiwan has no daylight-saving time. All day boundaries are computed on the server.
function taipeiDay(at) {
  const ms = Date.parse(at);
  if (!Number.isFinite(ms)) throw new Error('Invalid server timestamp');
  const date = new Date(ms + 8 * 3600000).toISOString().slice(0, 10);
  const start = Date.parse(date + 'T00:00:00+08:00');
  return { date, start, end: start + 86400000 };
}
function summary(rows, at) {
  const ms = Date.parse(at), day = taipeiDay(at);
  let today = 0, active = null;
  for (const row of rows) {
    const start = Date.parse(row.online_started_at);
    const end = Math.min(ms, Date.parse(row.online_ended_at || row.lease_until));
    today += Math.max(0, Math.min(end, day.end) - Math.max(start, day.start));
    if (!row.online_ended_at && Date.parse(row.lease_until) > ms) active = row;
  }
  return { status: active ? 'ONLINE' : 'OFFLINE', timezone: 'Asia/Taipei',
    serverNow: at, day: day.date, dayStartedAt: new Date(day.start).toISOString(),
    nextResetAt: new Date(day.end).toISOString(), todaySeconds: Math.floor(today / 1000),
    sessionSeconds: active ? Math.floor((ms - Date.parse(active.online_started_at)) / 1000) : 0,
    sessionId: active?.id || null, onlineStartedAt: active?.online_started_at || null,
    leaseUntil: active?.lease_until || null };
}
async function expire(store, userId, at) {
  await store.run(`UPDATE oms_online_sessions SET online_ended_at=lease_until
    WHERE user_id=? AND online_ended_at IS NULL AND lease_until<=?`, [userId, at]);
}
async function read(store, userId, at) {
  const day = taipeiDay(at);
  const rows = await store.all(`SELECT * FROM oms_online_sessions WHERE user_id=?
    AND (online_ended_at IS NULL OR online_ended_at>?)`, [userId, new Date(day.start).toISOString()]);
  const result=summary(rows, at);
  const history=await store.all('SELECT online_started_at,online_ended_at,lease_until FROM oms_online_sessions WHERE user_id=? AND online_started_at<=?', [userId,at]);
  result.streak=activityStreak(history,at);
  return result;
}
async function presence(store, userId, command = 'READ', at = new Date().toISOString()) {
  return store.transaction(async () => {
    await expire(store, userId, at);
    let active = await store.get('SELECT id FROM oms_online_sessions WHERE user_id=? AND online_ended_at IS NULL', [userId]);
    if (command === 'ONLINE' && !active) {
      await store.run('INSERT INTO oms_online_sessions(id,user_id,online_started_at,lease_until) VALUES(?,?,?,?)',
        [crypto.randomUUID(), userId, at, new Date(Date.parse(at) + LEASE_MS).toISOString()]);
    } else if ((command === 'HEARTBEAT' || command === 'ONLINE') && active) {
      await store.run('UPDATE oms_online_sessions SET lease_until=? WHERE id=?',
        [new Date(Date.parse(at) + LEASE_MS).toISOString(), active.id]);
    } else if (command === 'OFFLINE' && active) {
      await store.run('UPDATE oms_online_sessions SET online_ended_at=? WHERE id=?', [at, active.id]);
    }
    const result = await read(store, userId, at);
    await store.run('UPDATE oms_employees SET status=?,updated_at=? WHERE user_id=?', [result.status, at, userId]);
    return result;
  });
}
module.exports = { presence, summary, taipeiDay, LEASE_MS, activityStreak, STREAK_MIN_SECONDS };
