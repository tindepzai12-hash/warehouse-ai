using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cdm.Figma.UI;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace JarVis.Warehouse.UI {
 public enum PresenceState { Loading,Offline,Connecting,Online,Reconnecting,ConnectionLost }
 public sealed class ImportedPresence:MonoBehaviour {
  sealed class Card { public TMP_Text state,time,today,session,reset,action;public Image dot;public Button button; }
  readonly List<Card> cards=new List<Card>(); ImportedFigmaNavigation nav; OmsMobileApi.PresenceReply snapshot;
  double received,nextPoll,nextPaint;bool busy,background;string token;int generation;
  public PresenceState State {get;private set;}=PresenceState.Offline;
  public bool CanWork=>State==PresenceState.Online;
  public OmsMobileApi.PresenceReply Snapshot=>snapshot;
  public string TodayClock {get;private set;}="00:00:00";
  public void ClearCards(){cards.Clear();}
  public void AttachCompactCard(Transform root){Build(root);Paint();}
  public void Configure(ImportedFigmaNavigation owner){nav=owner;foreach(var screen in nav.screens.Where(s=>s.id=="126:8"||s.id=="85:14")){foreach(var node in screen.root.GetComponentsInChildren<FigmaNode>(true)){if(node.nodeName=="next-task-card")node.gameObject.SetActive(false);var label=node.GetComponent<TMP_Text>();if(label!=null&&(ImportedAppLocalization.Source(label)=="BẬT TRỰC TUYẾN"||ImportedAppLocalization.Source(label)=="ĐANG TRỰC TUYẾN"))label.transform.parent.gameObject.SetActive(false);}}Paint();}
  public void Restore(){generation++;StopAllCoroutines();busy=false;snapshot=null;token=OmsMobileSession.AccessToken;State=PresenceState.Loading;nextPoll=Time.realtimeSinceStartupAsDouble+30;StartCoroutine(Sync(false));}
  void Build(Transform root){var panel=Rect(root,"Online presence",0,0,366,70);var image=panel.gameObject.AddComponent<Image>();image.color=Color.white;UniformUiSurface.Use(image,19,PrototypeUi.Hex(nav.Settings.Dark?"192f2b":"f1fbf7"),PrototypeUi.Hex(nav.Settings.Dark?"385b4c":"c4e8da"),1,false,false);var card=new Card();cards.Add(card);
   card.dot=Rect(panel,"Presence indicator",17,30,9,9).gameObject.AddComponent<Image>();UniformUiSurface.Use(card.dot,5,Color.white,Color.clear,0,false,false);card.dot.raycastTarget=false;
   card.state=Text(panel,"Presence state",35,21,178,30,20);card.state.fontStyle=FontStyles.Bold;
   card.today=Text(panel,"Today label",18,54,144,22,13);card.time=Text(panel,"Today online",18,76,172,36,25);card.time.fontStyle=FontStyles.Bold;card.time.characterSpacing=0;card.time.richText=false;
   card.session=Text(panel,"Current session",18,104,340,0,13);card.session.gameObject.SetActive(false);
   card.reset=Text(panel,"Taiwan daily reset",200,64,160,46,11);card.reset.textWrappingMode=TextWrappingModes.Normal;
   var divider=Rect(panel,"Time divider",184,58,1,50).gameObject.AddComponent<Image>();divider.color=new Color(.75f,.85f,.86f);divider.raycastTarget=false;
   var target=Rect(panel,"Change online state",216,13,134,44);var bg=target.gameObject.AddComponent<Image>();bg.color=Color.white;UniformUiSurface.Use(bg,14,PrototypeUi.Hex(nav.Settings.Dark?"30435a":"dfe8f0d1"),PrototypeUi.Hex(nav.Settings.Dark?"66809f":"ffffffcc"),1,true,false);PrototypeGlassShadow.Attach(bg,14);card.button=target.gameObject.AddComponent<Button>();card.button.targetGraphic=bg;card.button.onClick.AddListener(()=>PrototypeDialog.Presence(nav));target.gameObject.AddComponent<ImportedPressFeedback>();
   card.today.gameObject.SetActive(false);card.time.gameObject.SetActive(false);card.session.gameObject.SetActive(false);card.reset.gameObject.SetActive(false);divider.gameObject.SetActive(false);
   PrototypeUi.Icon(target,"power",9,12,20,PrototypeUi.Ink(nav.Settings.Dark));card.action=Text(target,"Status action",33,0,94,44,12);card.action.alignment=TextAlignmentOptions.Center;card.action.color=PrototypeUi.Ink(nav.Settings.Dark);
  }
  static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;}
  TMP_Text Text(Transform p,string name,float x,float y,float w,float h,float size){var t=Rect(p,name,x,y,w,h).gameObject.AddComponent<TextMeshProUGUI>();t.font=nav.screens[0].root.GetComponentInChildren<TMP_Text>(true).font;t.fontSize=size;t.color=new Color(.12f,.19f,.31f);t.raycastTarget=false;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;return t;}
  public void Toggle(){if(busy)return;if(!OmsMobileSession.IsReady){nav.Notice(En?"Sign in before going online.":"Đăng nhập trước khi bật trực tuyến.");return;}if(State==PresenceState.ConnectionLost||State==PresenceState.Reconnecting){StartCoroutine(Sync(false));return;}StartCoroutine(Change(!CanWork));}
  IEnumerator Change(bool online){busy=true;State=online?PresenceState.Connecting:PresenceState.Reconnecting;Paint();int version=generation;OmsMobileApi.PresenceReply reply=null;yield return OmsMobileApi.SetPresence(online,r=>reply=r,e=>{});if(version!=generation)yield break;busy=false;if(reply==null){State=PresenceState.ConnectionLost;nav.Notice(En?"Couldn't update status. Check your connection and retry.":"Chưa cập nhật được trạng thái. Kiểm tra kết nối rồi thử lại.");nextPoll=Time.realtimeSinceStartupAsDouble+5;}else{Accept(reply);nav.Notice(reply.status=="ONLINE"?(En?"You're now online.":"Đã bật trực tuyến."):(En?"You're now offline.":"Đã tắt trực tuyến."));}Paint();}
  IEnumerator Sync(bool heartbeat){if(busy||!OmsMobileSession.IsReady)yield break;busy=true;int version=generation;OmsMobileApi.PresenceReply reply=null;yield return heartbeat?OmsMobileApi.Heartbeat(r=>reply=r,e=>{}):OmsMobileApi.GetPresence(r=>reply=r,e=>{});if(version!=generation)yield break;busy=false;if(reply==null){State=snapshot==null?PresenceState.ConnectionLost:PresenceState.Reconnecting;nextPoll=Time.realtimeSinceStartupAsDouble+5;}else Accept(reply);Paint();}
  void Accept(OmsMobileApi.PresenceReply reply){snapshot=reply;received=Time.realtimeSinceStartupAsDouble;State=reply.status=="ONLINE"?PresenceState.Online:PresenceState.Offline;nextPoll=received+30;if(nav.CurrentScreen=="126:8"||nav.CurrentScreen=="85:14")nav.Show(CanWork?"126:8":"85:14",false);Paint();}
  void Update(){double now=Time.realtimeSinceStartupAsDouble;if(token!=OmsMobileSession.AccessToken){token=OmsMobileSession.AccessToken;generation++;StopAllCoroutines();busy=false;snapshot=null;State=PresenceState.Offline;if(OmsMobileSession.IsReady)Restore();}if(!background&&OmsMobileSession.IsReady&&!busy&&now>=nextPoll){nextPoll=now+30;StartCoroutine(Sync(snapshot?.status=="ONLINE"));}if(now>=nextPaint){nextPaint=now+1;Paint();}}
  void OnApplicationPause(bool pause){background=pause;if(!pause&&OmsMobileSession.IsReady)nextPoll=0;}
  static bool En=>ImportedAppLocalization.IsEnglish;
  static string Clock(double seconds){int n=Math.Max(0,(int)seconds);return $"{n/3600:00}:{n/60%60:00}:{n%60:00}";}
  void Paint(){bool dark=nav?.Settings!=null&&nav.Settings.Dark;double elapsed=snapshot!=null&&CanWork?Time.realtimeSinceStartupAsDouble-received:0;double today=snapshot?.todaySeconds??0,session=snapshot?.sessionSeconds??0;
   if(snapshot!=null&&CanWork&&DateTimeOffset.TryParse(snapshot.serverNow,out var server)&&DateTimeOffset.TryParse(snapshot.nextResetAt,out var reset)&&DateTimeOffset.TryParse(snapshot.leaseUntil,out var lease)){double current=server.ToUnixTimeMilliseconds()+elapsed*1000;if(current>=lease.ToUnixTimeMilliseconds()){State=PresenceState.ConnectionLost;elapsed=0;nextPoll=0;}else if(current>=reset.ToUnixTimeMilliseconds()){today=(current-reset.ToUnixTimeMilliseconds())/1000;nextPoll=0;}else today+=elapsed;session+=elapsed;}
   string state=State==PresenceState.Online?(En?"Online":"Trực tuyến"):State==PresenceState.Connecting?(En?"Connecting…":"Đang kết nối…"):State==PresenceState.Loading?(En?"Checking…":"Đang kiểm tra…"):State==PresenceState.Reconnecting?(En?"Reconnecting…":"Kết nối lại…"):State==PresenceState.ConnectionLost?(En?"Connection lost":"Mất kết nối"):(En?"Offline":"Ngoại tuyến");
   TodayClock=snapshot==null&&State!=PresenceState.Offline?"--:--:--":Clock(today);
   foreach(var c in cards){if(c.state==null)continue;var ink=dark?JarVisDesignTokens.DarkInk:JarVisDesignTokens.Ink;c.state.text=state;c.state.color=CanWork?PrototypeUi.Hex(dark?"98deb8":"0d684c"):ink;c.time.text=snapshot==null&&State!=PresenceState.Offline?"--:--:--":Clock(today);c.time.color=ink;c.today.text=En?"Today":"Hôm nay";c.today.color=ink;c.session.text=(En?"Current session: ":"Phiên hiện tại: ")+Clock(session);c.session.color=ink;c.reset.text=En?"Resets at 00:00\nTaiwan (UTC+8)":"Đặt lại lúc 00:00\nĐài Loan (UTC+8)";c.reset.color=dark?new Color(.67f,.75f,.85f):JarVisDesignTokens.Secondary;c.dot.color=CanWork?new Color(.07f,.55f,.31f):new Color(.48f,.55f,.65f);c.action.text=CanWork?(En?"Go offline":"Tắt trực tuyến"):State==PresenceState.ConnectionLost||State==PresenceState.Reconnecting?(En?"Retry":"Thử lại"):(En?"Go online":"Bật trực tuyến");c.button.interactable=!busy;c.action.color=PrototypeUi.Ink(dark);c.action.fontStyle=FontStyles.Bold;float scale=ImportedSettings.TextScale;c.state.fontSize=20*scale;c.state.enableAutoSizing=true;c.state.fontSizeMax=20*scale;c.state.fontSizeMin=14;c.time.fontSize=25*scale;c.time.enableAutoSizing=true;c.time.fontSizeMax=25*scale;c.time.fontSizeMin=20;c.today.fontSize=13*scale;c.session.fontSize=13*scale;c.reset.fontSize=11*scale;c.action.fontSize=11*scale;c.action.enableAutoSizing=true;c.action.fontSizeMax=11*scale;c.action.fontSizeMin=10;}
  }
  public void SignOut(){if(busy){nav.Notice(En?"Wait for the status update.":"Chờ cập nhật trạng thái hoàn tất.");return;}StartCoroutine(EndSession());}
  IEnumerator EndSession(){busy=true;OmsMobileApi.Ack reply=null;if(OmsMobileSession.IsReady)yield return OmsMobileApi.Logout(r=>reply=r,e=>{});busy=false;if(OmsMobileSession.IsReady&&reply==null){nav.Notice(En?"Couldn't sign out. Reconnect and retry.":"Chưa đăng xuất được. Kết nối lại rồi thử lại.");yield break;}generation++;snapshot=null;State=PresenceState.Offline;OmsMobileSession.Clear();nav.Show("5:36",false);}
 }
}
