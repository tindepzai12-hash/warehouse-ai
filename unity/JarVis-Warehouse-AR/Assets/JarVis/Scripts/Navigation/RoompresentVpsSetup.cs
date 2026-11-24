using System;
using System.Collections.Generic;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Navigation
{
    // Installer-only QR registration. Book scans never modify map coordinates.
    public sealed class RoompresentVpsSetup : MonoBehaviour
    {
        SmallRoomAR guide;
        RoompresentQrLocalization qr;
        TMP_Text title,state,body,focusLabel,verifyLabel;
        Button verify;
        GameObject scanFrame;
        readonly List<Vector3> observed=new List<Vector3>();
        int qrRevision;
        bool preview,saved;
        float receivedAt=-100;
        string notice;
        bool English=>ImportedAppLocalization.IsEnglish;
        string T(string vi,string en)=>English?en:vi;

        public void Configure(SmallRoomAR value,TMP_FontAsset font)
        {
            guide=value;qr=RoompresentQrLocalization.Attach(guide);
            guide.pins.gameObject.SetActive(false);guide.path.ClearMesh();
            var go=new GameObject("Roompresent installation",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,gameObject.scene);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=310;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(412,915);scaler.matchWidthOrHeight=.5f;
            var safe=new GameObject("Safe area",typeof(RectTransform)).GetComponent<RectTransform>();safe.SetParent(go.transform,false);var area=Screen.safeArea;safe.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);safe.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;
            var dock=new GameObject("Setup dock",typeof(RectTransform)).GetComponent<RectTransform>();dock.SetParent(safe,false);dock.anchorMin=dock.anchorMax=new Vector2(.5f,0);dock.pivot=new Vector2(.5f,0);dock.anchoredPosition=new Vector2(0,12);dock.sizeDelta=new Vector2(378,290);
            var card=PrototypeUi.Panel(dock,"QR room setup",0,0,378,290,true,20);
            title=PrototypeUi.Text(card,font,"",16,12,346,28,18,true,true);
            state=PrototypeUi.Text(card,font,"",16,45,346,24,12,true,true);
            body=PrototypeUi.Text(card,font,"",16,76,346,92,13,true);
            body.textWrappingMode=TextWrappingModes.Normal;body.overflowMode=TextOverflowModes.Truncate;
            verify=PrototypeUi.Action(card,font,T("Ghim đã khớp · Lưu","Pins match · Save"),16,178,346,40,true,Verify,true);
            verifyLabel=verify.GetComponentInChildren<TMP_Text>();
            PrototypeUi.Action(card,font,T("Làm lại A → B → C","Restart A → B → C"),16,236,166,38,true,Restart);
            PrototypeUi.Action(card,font,T("Về JarVis","Back to JarVis"),194,236,168,38,true,RoompresentQuickMapLauncher.Close);
            var frame=new GameObject("QR scan frame",typeof(RectTransform)).GetComponent<RectTransform>();frame.SetParent(go.transform,false);frame.anchorMin=frame.anchorMax=frame.pivot=new Vector2(.5f,.5f);frame.sizeDelta=new Vector2(220,220);scanFrame=frame.gameObject;
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
            {Corner(frame,new Vector2(x*100,y*110),new Vector2(24,3));Corner(frame,new Vector2(x*110,y*100),new Vector2(3,24));}
            var focusDock=new GameObject("Camera focus",typeof(RectTransform)).GetComponent<RectTransform>();focusDock.SetParent(safe,false);focusDock.anchorMin=focusDock.anchorMax=focusDock.pivot=Vector2.one;focusDock.anchoredPosition=new Vector2(-16,-16);focusDock.sizeDelta=new Vector2(166,40);
            focusLabel=PrototypeUi.Action(focusDock,font,T("Nét: Tự động","Focus: Auto"),0,0,166,40,true,guide.localization.ToggleFocus).GetComponentInChildren<TMP_Text>();
        }
        static void Corner(RectTransform parent,Vector2 position,Vector2 size)
        {
            var rect=new GameObject("Scan corner",typeof(RectTransform),typeof(Image)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;
            var graphic=rect.GetComponent<Image>();graphic.color=new Color(.3f,1,.8f);graphic.raycastTarget=false;
        }
        void Update()
        {
            if(guide==null)return;
            focusLabel.text=guide.localization.AutoFocus?T("Nét: Tự động","Focus: Auto"):T("Nét: Toàn phòng","Focus: Room");
            if(observed.Count>0&&qr.Revision!=qrRevision)
            {observed.Clear();preview=saved=false;notice=T("Đã tìm lại A; cần quét lại B và C.","A was reacquired; scan B and C again.");qr.RequestSetupPoint(null);}
            if(observed.Count==0&&qr.HasAnchor)
            {observed.Add(Vector3.zero);qrRevision=qr.Revision;receivedAt=Time.unscaledTime;notice=T("✓ Đã đo và nhận mốc A","✓ Fixed A measured and recognized");}
            if(!preview&&!saved&&qr.HasAnchor&&observed.Count>0&&observed.Count<3)
            {
                string point=observed.Count==1?"B":"C";
                if(qr.TryConsumeSetupPoint(point,out var position))
                {
                    observed.Add(position);qr.RequestSetupPoint(null);receivedAt=Time.unscaledTime;notice=T("✓ Đã quét và ghi ","✓ Scanned and measured ")+point;
                    if(observed.Count==3)Preview();
                }
                else qr.RequestSetupPoint(point);
            }
            bool located=qr.HasAnchor;
            string expected=!located||observed.Count==0?"A":observed.Count==1?"B":"C";
            title.text=saved?T("Đã lưu mốc A / B / C","A / B / C saved"):preview?T("Kiểm tra ghim trong phòng","Check pins in the room"):!located&&observed.Count>0?T("Đưa QR A vào hình để tìm lại mốc","Show QR A to recover tracking"):T("Quét QR ","Scan QR ")+expected+" · "+(Mathf.Min(observed.Count,2)+1)+"/3";
            state.text=saved?T("✓ Sẵn sàng cho nhiệm vụ giao hai sách","✓ Ready for the two-book delivery"):preview?T("✓ Đã đo đủ ba QR","✓ All three QR positions measured"):qr.CameraReady?(located?T("A đã giữ cố định · đang quét ","Fixed A tracked · scanning ")+expected:T("Đang đo mốc A trên mặt bàn","Measuring fixed A on the table")):T("Đang tìm lại tracking · giữ máy chắc","Recovering tracking · hold the phone steady");
            if(Time.unscaledTime-receivedAt<2.5f&&notice!=null)state.text=notice;
            state.color=located?new Color(.3f,1,.8f):new Color(1,.8f,.4f);
            body.text=saved?T("Đã lưu B/C theo QR A. Lần sau quét hai sách rồi QR A để dẫn đường. Giữ nguyên các mốc và hai xe.","B/C saved relative to fixed A. Next time scan both books, then QR A for directions. Keep all markers and carts fixed."):preview?T("Đi ngang và xoay máy để đối chiếu cả ba ghim với vật thật. Chỉ lưu khi các ghim khớp. Nếu lệch, đặt lại QR đúng điểm trên từng xe rồi quét lại.","Step sideways and turn to check all three pins against real objects. Save only when they match. Otherwise place the QRs on the correct cart points and rescan."):expected=="A"?T("Đưa trọn QR A (E cũ) và mặt bàn vào hình. Giữ máy yên; app tự nhận và chuyển sang B.","Show the whole QR A (old E) and table surface. Hold still; recognition advances to B automatically."):expected=="B"?T("Đặt QR B nằm phẳng trên mặt xe sách trái. Đưa trọn mã và vùng mặt kệ vào hình, giữ yên. App tự ghi B.","Place QR B flat on the left cart. Show the whole code and surrounding surface, then hold still. B is captured automatically."):T("Đặt QR C nằm phẳng trên mặt xe sách phải. Đưa trọn mã và vùng mặt kệ vào hình, giữ yên. App tự ghi C.","Place QR C flat on the right cart. Show the whole code and surrounding surface, then hold still. C is captured automatically.");
            if(!preview&&!saved)
            {
                if(qr.Message=="WRONG_POINT")body.text+="\n"+T("Đang thấy ","Seeing ")+qr.LastReadPoint+T("; cần QR ","; expected QR ")+expected+".";
                else if(qr.Message=="SHOW_SURFACE")body.text+="\n"+T("Đã đọc mã; cần thấy rõ cả mặt phẳng quanh mã.","Code read; show the surrounding flat surface too.");
                else if(qr.Message=="HOLD_STILL"||qr.Message=="STABILIZING")body.text+="\n"+T("Giữ máy yên · ","Hold still · ")+qr.StableSamples+"/4";
            }
            guide.pins.gameObject.SetActive(located&&(preview||saved));scanFrame.SetActive(!preview&&!saved);
            verify.gameObject.SetActive(preview||saved);verify.interactable=located;
            verifyLabel.text=saved?T("Bắt đầu dẫn đường","Start navigation"):T("Ghim đã khớp · Lưu","Pins match · Save");
            if(preview||saved)SmallRoomAR.FacePins(guide.pins,guide.localization.cameraManager.transform);
        }
        void Preview()
        {
            if(!RoompresentDeliveryAR.TryFit(guide.registrationLandmarks,observed.ToArray(),out var translation,out float yaw,out float error))
            {
                observed.RemoveRange(1,observed.Count-1);receivedAt=Time.unscaledTime;
                notice=T("Ba mốc chưa khớp sơ đồ. Đặt B ở xe trái, C ở xe phải đúng vị trí trong bản quét; quét lại B/C.","Landmarks do not fit the room scan. Put B on the left cart and C on the right cart at their scanned positions; rescan B/C.");
                Debug.LogWarning("[RoompresentQR] B/C QR measurements rejected by the rigid map fit. Registration was not saved.");return;
            }
            qr.ApplyPreview(translation,yaw);preview=true;qr.RequestSetupPoint(null);
            notice=T("Sai số khớp nội bộ: ","Internal fit residual: ")+error.ToString("F2")+" m";
            Debug.Log("[RoompresentQR] All three QR landmarks measured; operator check required; fit="+error.ToString("F3"));
        }
        void Verify()
        {
            if(saved){RoompresentQuickMapLauncher.ResumeCurrent();return;}
            if(!preview||!qr.HasAnchor)return;
            try{if(qr.SaveChecked(observed.ToArray())){saved=true;notice=null;}}
            catch(Exception){notice=T("Chưa lưu được; thử lại.","Could not save; try again.");}
        }
        void Restart()
        {observed.Clear();preview=saved=false;notice=null;receivedAt=-100;qr.RequestSetupPoint(null);guide.pins.gameObject.SetActive(false);}
        void OnDestroy(){if(qr!=null)qr.RequestSetupPoint(null);}
    }
}
