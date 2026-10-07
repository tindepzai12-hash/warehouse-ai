const {test}=require('node:test');
const assert=require('node:assert/strict');
const {activityStreak}=require('../oms/presence');
const row=(start,end,lease=end)=>({online_started_at:start,online_ended_at:end,lease_until:lease});
test('streak uses recorded online time, not login, and sums interrupted sessions',()=>{
 const at='2026-10-06T08:00:00Z';
 assert.equal(activityStreak([],at).currentStreak,0);
 const rows=[row('2026-10-06T01:00:00Z','2026-10-06T01:00:30Z'),row('2026-10-06T02:00:00Z','2026-10-06T02:00:30Z')];
 const r=activityStreak(rows,at);assert.equal(r.currentStreak,1);assert.equal(r.todayQualified,true);assert.equal(r.minimumSeconds,60);
 assert.equal(r.week.filter(d=>d.qualified).length,1);assert.equal(r.scheduleAware,false);
});
test('overlapping sessions cannot double count a qualification minute',()=>{
 const rows=[row('2026-10-06T01:00:00Z','2026-10-06T01:00:40Z'),row('2026-10-06T01:00:10Z','2026-10-06T01:00:50Z')];
 assert.equal(activityStreak(rows,'2026-10-06T08:00:00Z').todayQualified,false);
});
test('today is pending until qualified, preserving yesterday streak until day ends',()=>{
 const rows=[row('2026-10-04T01:00:00Z','2026-10-04T01:02:00Z'),row('2026-10-05T01:00:00Z','2026-10-05T01:02:00Z')];
 assert.equal(activityStreak(rows,'2026-10-06T01:00:00Z').currentStreak,2);
 assert.equal(activityStreak(rows,'2026-10-07T01:00:00Z').currentStreak,0);
});
test('Taiwan midnight splits sessions, caps current time at lease and never future time',()=>{
 const rows=[row('2026-10-06T15:59:00Z',null,'2026-10-06T16:02:00Z')];
 const justMidnight=activityStreak(rows,'2026-10-06T16:00:00Z');assert.equal(justMidnight.currentStreak,1);assert.equal(justMidnight.todayQualified,false);
 const after=activityStreak(rows,'2026-10-06T16:01:00Z');assert.equal(after.currentStreak,2);assert.equal(after.todayQualified,true);
 assert.equal(activityStreak([row('2026-10-06T01:00:00Z',null,'2026-10-06T01:00:59Z')],'2026-10-06T08:00:00Z').currentStreak,0);
 assert.equal(activityStreak([row('2026-10-07T01:00:00Z','2026-10-07T02:00:00Z')],'2026-10-06T08:00:00Z').currentStreak,0);
});
test('weekly indicators are Monday-first in Taipei and independent of host timezone',()=>{
 const r=activityStreak([row('2026-10-04T16:00:00Z','2026-10-04T16:01:00Z')],'2026-10-06T01:00:00Z');
 assert.equal(r.week[0].date,'2026-10-05');assert.equal(r.week[0].qualified,true);assert.equal(r.week[6].date,'2026-10-11');
});
