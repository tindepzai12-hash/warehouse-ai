using JarVis.Warehouse.UI;
using JarVis.Warehouse.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Navigation
{
    // Existing AR camera stays alive throughout destination + book verification.
    public sealed class RoompresentDeliveryHud:MonoBehaviour
    {
        SmallRoomAR guide;
        bool dark;
        RectTransform start, dock, proof, scanFrame, success, deliveryRows, completionOverlay,trackingLost;
        bool completionAcknowledged;
        bool hadTracking,lossAcknowledged;
        float lossSince=-1;
        TMP_Text scanLabel,successTitle,successBody;
        Image scanFill;
        RoompresentCodeFrame codeFrame;
        RectTransform scanCaption;
        Button focus,returnA,cameraRefresh;
        TMP_Text returnText;
        TMP_Text focusText;
        TMP_Text startTitle,startBody,progress,directionHint,proofTitle,proofBody,feedback;
        readonly Button[] route=new Button[2],confirm=new Button[2];
        readonly TMP_Text[] routeText=new TMP_Text[2],confirmText=new TMP_Text[2];
        bool En=>ImportedAppLocalization.IsEnglish;
        string T(string vi,string en)=>En?en:vi;
        public void Configure(SmallRoomAR owner,TMP_FontAsset font)
        {
            guide=owner;dark=ImportedSettings.CurrentDark;
            BuildScanner(font);
            var exit=PrototypeUi.Action(transform,font,T("Thoát","Exit"),16,12,64,48,dark,guide.ExitGuidance);exit.name="Exit AR";
            // Keep the existing font and controls; only the exit surface is red.
            UniformUiSurface.Use(exit.GetComponent<Image>(),14,PrototypeUi.Hex("c52b43"),PrototypeUi.Hex("e66b7c"),1,true,false);
            exit.GetComponentInChildren<TMP_Text>().color=Color.white;
            var again=PrototypeUi.Action(transform,font,"",92,12,48,48,dark,guide.RefreshDeliveryCamera);again.name="Refresh AR camera";
            cameraRefresh=again;
            var icon=new GameObject("Two rotating arrows",typeof(RectTransform),typeof(CanvasRenderer),typeof(ImportedUtilityIcon));
            icon.transform.SetParent(again.transform,false);var iconRect=(RectTransform)icon.transform;iconRect.anchorMin=iconRect.anchorMax=iconRect.pivot=new Vector2(.5f,.5f);iconRect.anchoredPosition=Vector2.zero;iconRect.sizeDelta=new Vector2(22,22);
            var graphic=icon.GetComponent<ImportedUtilityIcon>();graphic.symbol="refresh-arrows";graphic.color=PrototypeUi.Ink(dark);graphic.raycastTarget=false;
            start=BottomPanel("Scan start QR A",378,126,12);
            startTitle=PrototypeUi.Text(start,font,"",16,12,346,28,18,dark,true);
            startBody=PrototypeUi.Text(start,font,"",16,48,346,66,13,dark);
            dock=BottomPanel("Delivery actions",378,208,12);
            progress=PrototypeUi.Text(dock,font,"",16,10,260,32,20,dark,true);
            directionHint=PrototypeUi.Text(dock,font,"",16,44,346,24,12,dark);
            directionHint.color=PrototypeUi.Muted(dark);
            returnA=PrototypeUi.Action(dock,font,T("Về A","Return A"),284,9,78,48,dark,ReturnToA);
            returnA.name="Return to A";returnText=returnA.GetComponentInChildren<TMP_Text>();
            deliveryRows=JarVisUi.Rect(dock,"Pending delivery rows",0,78,378,120);
            for(int i=0;i<2;i++)
            {
                string stop=i==0?"B":"C";float y=i*60;
                var row=PrototypeUi.Panel(deliveryRows,"Delivery "+stop,12,y,354,56,dark,12,dark?"23354a":"f1f6fc",dark?"354b63":"dce5f0");
                route[i]=PrototypeUi.Action(row,font,"",4,4,78,48,dark,()=>guide.SelectDelivery(stop));
                route[i].name="Navigate "+stop;routeText[i]=route[i].GetComponentInChildren<TMP_Text>();
                confirm[i]=PrototypeUi.Action(row,font,"",90,4,260,48,dark,()=>guide.BeginDelivery(stop),true);
                confirm[i].name="Confirm "+stop;confirmText[i]=confirm[i].GetComponentInChildren<TMP_Text>();
            }
            proof=BottomPanel("Delivery verification",378,188,12);
            proofTitle=PrototypeUi.Text(proof,font,"",16,12,346,28,17,dark,true);
            proofBody=PrototypeUi.Text(proof,font,"",16,48,346,52,14,dark);
            feedback=PrototypeUi.Text(proof,font,"",16,100,346,38,11,dark);feedback.color=PrototypeUi.Hex(dark?"ffcc66":"875100");
            PrototypeUi.Action(proof,font,T("Hủy xác nhận","Cancel confirmation"),16,138,346,44,dark,guide.CancelDelivery);
            foreach(var b in route){var effect=b.GetComponent<ImportedPressFeedback>();if(effect!=null)effect.enabled=false;}
            foreach(var b in confirm){var effect=b.GetComponent<ImportedPressFeedback>();if(effect!=null)effect.enabled=false;}
            success=BottomPanel("QR scan completed",346,174,216);
            successTitle=PrototypeUi.Text(success,font,T("Quét QR hoàn thành","QR scan complete"),16,14,314,28,18,dark,true);
            successBody=PrototypeUi.Text(success,font,"",16,50,314,50,14,dark);
            PrototypeUi.Action(success,font,"OK",16,112,314,48,dark,guide.AcknowledgeQrSuccess,true).name="Acknowledge QR";
            BuildCompletion(font);
            BuildTrackingLoss(font);
            Refresh();
        }
        void BuildTrackingLoss(TMP_FontAsset font)
        {
            trackingLost=JarVisUi.Rect(transform,"JarVis tracking lost overlay",0,0,412,915);
            trackingLost.anchorMin=Vector2.zero;trackingLost.anchorMax=Vector2.one;trackingLost.offsetMin=trackingLost.offsetMax=Vector2.zero;
            var scrim=trackingLost.gameObject.AddComponent<Image>();scrim.color=new Color(0,0,0,.2f);scrim.raycastTarget=true;
            var card=PrototypeUi.Panel(trackingLost,"Tracking lost card",0,0,322,286,dark,22);
            card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);card.anchoredPosition=Vector2.zero;
            // The same approved artwork and native expression system; only the popup face is worried.
            var logo=JarVisUi.Rect(card,"Original JarVis · worried face",117,4,88,86).gameObject.AddComponent<Image>();logo.sprite=Resources.Load<Sprite>("Prototype/JarVisExpressionBase");logo.preserveAspect=true;logo.raycastTarget=false;
            var face=JarVisUi.Rect(logo.transform,"Worried expression",0,0,66,64);face.localScale=Vector3.one*(88f/66f);
            var left=PrototypeUi.Panel(face,"Worried left eye",24,35,6,9,false,3,"effaff","00000000");left.localRotation=Quaternion.Euler(0,0,-16);
            var right=PrototypeUi.Panel(face,"Worried right eye",33,35,6,9,false,3,"effaff","00000000");right.localRotation=Quaternion.Euler(0,0,16);
            var frown=JarVisUi.Rect(face,"Small worried frown",27,45,16,8).gameObject.AddComponent<MascotSmileArc>();frown.color=PrototypeUi.Hex("effaff");frown.raycastTarget=false;
            var title=PrototypeUi.Text(card,font,T("ĐÃ MẤT ĐỊNH VỊ","POSITION LOST"),18,92,286,30,20,dark,true);title.alignment=TextAlignmentOptions.Center;
            var body=PrototypeUi.Text(card,font,T("Vui lòng quét lại QR A để tiếp tục.\nTiến độ nhiệm vụ vẫn được giữ.","Scan QR A again to continue.\nYour task progress is retained."),20,134,282,58,14,dark);body.alignment=TextAlignmentOptions.Center;
            PrototypeUi.Action(card,font,"OK",20,218,282,48,dark,ResumeFromA,true).name="Acknowledge tracking loss";
            trackingLost.gameObject.SetActive(false);
        }
        void ResumeFromA(){lossAcknowledged=true;trackingLost.gameObject.SetActive(false);guide.RescanStart();Refresh();}
        void UpdateTrackingNotice(bool trusted,bool reacquiring,bool refreshing,float now)
        {
            if(trusted){hadTracking=true;lossAcknowledged=false;lossSince=-1;trackingLost.gameObject.SetActive(false);return;}
            if(!hadTracking||reacquiring||refreshing){lossSince=-1;trackingLost.gameObject.SetActive(false);return;}
            if(lossSince<0)lossSince=now;
            bool show=!lossAcknowledged&&now-lossSince>=1.5f;
            trackingLost.gameObject.SetActive(show);if(show)trackingLost.SetAsLastSibling();
        }
        void BuildCompletion(TMP_FontAsset font)
        {
            // A UGUI overlay only: the camera, AR session, anchors and map stay alive.
            completionOverlay=JarVisUi.Rect(transform,"Mission completion overlay",0,0,412,915);
            completionOverlay.anchorMin=Vector2.zero;completionOverlay.anchorMax=Vector2.one;
            completionOverlay.offsetMin=completionOverlay.offsetMax=Vector2.zero;
            var scrim=completionOverlay.gameObject.AddComponent<Image>();scrim.color=new Color(0,0,0,.2f);scrim.raycastTarget=true;
            var card=PrototypeUi.Panel(completionOverlay,"Mission complete card",0,0,346,306,dark,22);
            card.anchorMin=card.anchorMax=card.pivot=new Vector2(.5f,.5f);card.anchoredPosition=new Vector2(0,8);
            var close=PrototypeUi.Action(card,font,"",292,10,44,44,dark,CloseCompletion);
            close.name="Close mission completion";
            UniformUiSurface.Use(close.GetComponent<Image>(),12,PrototypeUi.Hex("fff0f2"),PrototypeUi.Hex("fff0f2"),0,false,false);
            var x=JarVisUi.Icon(close.transform,"x",12,12,20);x.color=PrototypeUi.Hex("c52b43");
            var circle=PrototypeUi.Panel(card,"Success",142,21,62,62,false,31,"00965f","00965f");
            var check=JarVisUi.Icon(circle,"check",16,16,30);check.color=Color.white;
            var title=PrototypeUi.Text(card,font,T("ĐÃ HOÀN THÀNH\nNHIỆM VỤ","TASK\nCOMPLETED"),16,94,314,70,23,dark,true);
            title.alignment=TextAlignmentOptions.Center;
            var task=PrototypeUi.Text(card,font,T("#00001 · CẤT HÀNG","#00001 · PUTAWAY"),16,173,314,26,12,dark,true);
            task.alignment=TextAlignmentOptions.Center;task.color=PrototypeUi.Muted(dark);
            PrototypeUi.Action(card,font,T("Về A","Return A"),20,226,306,50,dark,ReturnToA,true).name="Return A from completion";
            completionOverlay.gameObject.SetActive(false);
        }
        void CloseCompletion(){completionAcknowledged=true;Refresh();}
        void ReturnToA(){completionAcknowledged=true;guide.ReturnToStart();Refresh();}
        RectTransform BottomPanel(string name,float width,float height,float bottom)
        {
            var panel=PrototypeUi.Panel(transform,name,0,0,width,height,dark,22);
            panel.anchorMin=panel.anchorMax=new Vector2(.5f,0);panel.pivot=new Vector2(.5f,0);
            panel.anchoredPosition=new Vector2(0,bottom);return panel;
        }
        void BuildScanner(TMP_FontAsset font)
        {
            focus=PrototypeUi.Action(transform,font,"A",152,12,48,48,dark,guide.RescanStart);
            focus.name="Rescan QR A";focusText=focus.GetComponentInChildren<TMP_Text>();focusText.fontSize=18;
            scanFrame=new GameObject("Code scan frame",typeof(RectTransform)).GetComponent<RectTransform>();scanFrame.SetParent(transform,false);
            scanFrame.anchorMin=scanFrame.anchorMax=new Vector2(.5f,.59f);scanFrame.pivot=new Vector2(.5f,.5f);scanFrame.sizeDelta=new Vector2(244,220);
            codeFrame=scanFrame.gameObject.AddComponent<RoompresentCodeFrame>();codeFrame.SetBarcode(false);
            var caption=PrototypeUi.Panel(scanFrame,"Scan progress",-32,246,300,62,dark,14);scanCaption=caption;
            scanLabel=PrototypeUi.Text(caption,font,"",12,5,276,36,12,dark,true);scanLabel.alignment=TextAlignmentOptions.Center;
            var track=PrototypeUi.Panel(caption,"Progress track",14,47,272,5,dark,2,dark?"445770":"dce5f0","00000000");
            var fill=new GameObject("Measured progress",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));fill.transform.SetParent(track,false);
            var rect=(RectTransform)fill.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            scanFill=fill.GetComponent<Image>();scanFill.color=PrototypeUi.Hex(dark?"68e6bb":"008755");scanFill.raycastTarget=false;
        }
        void Update(){if(guide!=null)Refresh();}
        public void Refresh()
        {
            var record=RoompresentDeliveryMission.Current;
            if(!record.Complete)completionAcknowledged=false;
            bool ready=guide.HasStartFix,awaiting=guide.PendingQrSuccess!=null;
            bool showCompletion=record.Complete&&!completionAcknowledged&&!guide.ReturningToStart&&(ready||guide.StartAligned);
            completionOverlay.gameObject.SetActive(showCompletion);
            if(showCompletion)completionOverlay.SetAsLastSibling();
            success.gameObject.SetActive(awaiting&&ready);
            bool rescan=guide.qrLocalization!=null&&guide.qrLocalization.ReacquiringA;
            bool recovering=guide.StartAligned&&guide.qrLocalization!=null&&guide.qrLocalization.HasReference&&!rescan;
            bool barcode=guide.Verification.Active&&guide.Verification.WaitingForProduct;
            codeFrame.SetBarcode(barcode);
            scanCaption.anchoredPosition=new Vector2(barcode?0:-32,barcode?-142:-246);
            bool scanning=(!ready&&!recovering||guide.Verification.Active)&&!awaiting;
            scanFrame.gameObject.SetActive(scanning);
            scanLabel.text=!guide.Verification.Active ? T("QR A · Giữ yên để căn vị trí","QR A · Hold steady to align")+"\n"+(guide.qrLocalization==null?0:guide.qrLocalization.StableSamples)+"/4" : guide.Verification.WaitingForProduct?T("Bước 2/2 · Barcode ","Step 2/2 · Barcode ")+guide.Verification.ProductName : T("Bước 1/2 · QR ","Step 1/2 · QR ")+guide.Verification.Stop+" · "+(guide.DeliveryFeedback!=null&&guide.DeliveryFeedback.Contains(T("vị trí đang lệch","position mismatch"))?T("Đã đọc · cần căn vị trí","Read · realign position"):Mathf.RoundToInt(guide.ScanProgress*100)+"%");
            scanFill.rectTransform.anchorMax=new Vector2(Mathf.Clamp01(guide.ScanProgress),1);
            cameraRefresh.interactable=guide.localization==null||!guide.localization.FocusRefreshing;
            focusText.text="A";
            successBody.text=guide.PendingQrSuccess=="A"?T("Đã căn bản đồ. Bấm OK để dẫn đường.","Room aligned. Tap OK for directions."):(guide.DestinationPositionUpdated?T("Đã cập nhật vị trí tại ","Position updated at "):T("Đã xác thực QR ","QR verified at "))+guide.PendingQrSuccess+T(". Bấm OK rồi quét barcode ",". Tap OK, then scan barcode ")+guide.Verification.ProductName+".";
            start.gameObject.SetActive(!ready);dock.gameObject.SetActive(ready&&!guide.Verification.Active);proof.gameObject.SetActive(ready&&guide.Verification.Active&&!awaiting);
            if(awaiting&&ready){dock.gameObject.SetActive(false);success.SetAsLastSibling();}
            startTitle.text=rescan?T("Quét lại QR A","Rescan QR A"):guide.StartAligned?T("Đang nối lại vị trí","Recovering position"):T("Quét QR A để bắt đầu","Scan QR A to begin");
            startBody.text=rescan?T("Đưa QR A cố định vào khung. Nhiệm vụ và hai điểm giao vẫn được giữ.","Show the fixed QR A. Your task and both destinations are retained."):recovering?T("Giữ máy chắc, hướng camera vào kệ và sàn. App đang nối lại vị trí; tiến độ vẫn được lưu.","Hold steady and show the shelf and floor. Position is reconnecting; progress is saved."):guide.StartAligned?T("Giữ máy chắc. Tiến độ vẫn được lưu; đưa QR A vào hình nếu cần căn lại.","Hold steady. Progress is saved; show QR A if realignment is needed."):T("Đưa QR A cố định trên bàn vào camera và giữ yên. Dẫn đường chỉ mở sau khi căn thành công.","Show the fixed QR A on the table and hold steady. Directions open after alignment succeeds.");
            deliveryRows.gameObject.SetActive(!record.Complete);
            dock.sizeDelta=new Vector2(378,record.Complete?88:208);
            returnA.interactable=guide.DeliveryTracking&&!guide.Verification.Active&&!guide.AtStart;
            returnText.text=guide.AtStart?T("Đã về A","At A"):T("Về A","Return A");
            progress.text=guide.ReturningToStart?(guide.AtStart?T("Đã về bàn A","Back at table A"):T("Về bàn A","Return to A")+(float.IsFinite(guide.ReturnDistance)?" · "+guide.ReturnDistance.ToString("F1")+" m":"")):record.Complete?T("Đã hoàn thành","Completed"):T("Giao sách · ","Deliver books · ")+(record.deliveredB||record.deliveredC?"1/2":"0/2");
            directionHint.text=guide.RouteMissing?T("Lùi nhẹ về lối đi để nối đường.","Step back into the aisle to reconnect."):guide.ReturningToStart?T("Đi theo mũi tên để về A.","Follow the arrows back to A."):record.Complete?T("Chọn Về A để về bàn xuất phát.","Choose Return A to go back to the start."):T("A1 → B · A2 → C","A1 → B · A2 → C");
            for(int i=0;i<2;i++)
            {
                string stop=i==0?"B":"C",book=i==0?"A1":"A2";bool done=i==0?record.deliveredB:record.deliveredC;
                routeText[i].text=(guide.SelectedStop==stop?"→ ":"")+T("Tới ","Go to ")+stop;
                confirmText[i].text=done?T("Đã giao ","Delivered ")+book+T(" tại "," at ")+stop:T("Xác nhận đã giao ","Confirm delivery of ")+book+T(" tại "," at ")+stop;
                route[i].interactable=!done&&!guide.Verification.Active;
                confirm[i].interactable=!done&&guide.DeliveryTracking&&!guide.Verification.Active;
                // Surface sprites preserve brand gradients; group alpha and grey labels expose disabled state.
                foreach(var b in new[]{route[i],confirm[i]})
                {
                    var group=b.GetComponent<CanvasGroup>();if(group==null)group=b.gameObject.AddComponent<CanvasGroup>();
                    group.alpha=done?.85f:b.interactable?1:.65f;
                    if(done&&!b.gameObject.name.EndsWith(" done"))
                    {
                        b.gameObject.name+=" done";
                        UniformUiSurface.Use(b.GetComponent<Image>(),12,PrototypeUi.Hex(dark?"4c5665":"e7ecf2"),PrototypeUi.Hex(dark?"677386":"ccd6e3"),1,false,false);
                    }
                }
                if(done){confirmText[i].color=PrototypeUi.Hex(dark?"ccd1d9":"43536b");routeText[i].color=confirmText[i].color;}
            }
            UpdateTrackingNotice(guide.DeliveryTracking,rescan,guide.localization!=null&&guide.localization.FocusRefreshing,Time.unscaledTime);
            if(!guide.Verification.Active)return;
            string target=guide.Verification.Stop,product=guide.Verification.ProductName;
            proofTitle.text=T("Xác nhận ","Confirm ")+product+" → "+target;
            proofBody.text=guide.Verification.WaitingForProduct?
                T("QR ","QR ")+target+T(" đã đúng\nBước 2/2 · Quét barcode "," verified\nStep 2/2 · Scan barcode ")+product:
                T("Bước 1/2 · Quét QR tại ","Step 1/2 · Scan QR at ")+target+T("\nSau đó quét barcode ","\nThen scan barcode ")+product;
            feedback.text=guide.DeliveryFeedback??T("Giữ mã trong hình để quét.","Keep the code in view.");
        }
    }
}
