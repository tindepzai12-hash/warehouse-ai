using System;
using System.Collections;
using System.IO;
using JarVis.Warehouse.Backend;
using JarVis.Warehouse.Localization;
using JarVis.Warehouse.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace JarVis.Warehouse.Navigation
{
    // This UI owns media and panels only. It never resets AR, changes an anchor or completes a delivery.
    public sealed class RoompresentArAssistant:MonoBehaviour
    {
        SmallRoomAR guide;
        bool dark;
        TMP_FontAsset font;
        RectTransform safe,pill,widget,widgetActions,chat,report,mapView,content,composer,viewport;
        TMP_InputField input,incident;
        TMP_Text state,chatState,reportState,micLabel,chatMicLabel;
        Button send,mic;
        ChatActionIcon widgetMicIcon;
        ScrollRect scroll;
        VoiceRecorder recorder;
        AudioSource speaker;
        OmsMobileApi.SpeechDownload speech;
        RoompresentAssistantMotion motion,chatMotion;
        RectTransform eyeL,eyeR,chatEyeL,chatEyeR;
        Texture2D map;
        RawImage mapFull;
        float messageY;
        readonly float[] outputLevels=new float[48],samples=new float[256];
        bool opened,busy,permissionPending,wasVerifying;
        int generation;
        string previous="";
        bool En=>ImportedAppLocalization.IsEnglish;
        string T(string vi,string en)=>En?en:vi;
        public bool MediaBusy=>busy||permissionPending||recorder!=null&&recorder.State!=VoiceRecorderState.Idle;

        public void Configure(SmallRoomAR owner,TMP_FontAsset asset)
        {
            guide=owner;font=asset;dark=ImportedSettings.CurrentDark;safe=(RectTransform)transform;
            recorder=gameObject.AddComponent<VoiceRecorder>();recorder.Failed+=Error;
            speaker=gameObject.AddComponent<AudioSource>();speaker.playOnAwake=false;speaker.spatialBlend=0;
            // Transparent hit target; the original brand image is the only visible launcher.
            pill=JarVisUi.Rect(transform,"JarVis assistant launcher",16,72,72,72);
            PrototypeUi.Button(pill,()=>OpenWidget());
            var logo=JarVisUi.Rect(pill,"JarVis",4,4,64,64).gameObject.AddComponent<Image>();logo.sprite=Resources.Load<Sprite>("Prototype/JarVis");logo.preserveAspect=true;logo.raycastTarget=false;
            BuildWidget();BuildChat();BuildReport();BuildMap();
            widget.gameObject.SetActive(false);chat.gameObject.SetActive(false);report.gameObject.SetActive(false);mapView.gameObject.SetActive(false);
        }
        RectTransform Card(string name,float height,float bottom)
        {
            var r=PrototypeUi.Panel(transform,name,0,0,378,height,dark,22,dark?"17273b":"ffffff",dark?"526b87":"dce5f0");
            r.GetComponent<Image>().raycastTarget=true;r.anchorMin=r.anchorMax=new Vector2(.5f,0);r.pivot=new Vector2(.5f,0);r.anchoredPosition=new Vector2(0,bottom);return r;
        }
        Button Action(Transform parent,string text,float x,float y,float w,Action callback,bool primary=false)
        {return PrototypeUi.Action(parent,font,text,x,y,w,48,dark,callback,primary);}
        void RedClose(Transform parent,float x,float y,Action callback)
        {
            var b=Action(parent,"",x,y,48,callback);UniformUiSurface.Use(b.GetComponent<Image>(),24,PrototypeUi.Hex("dc3048"),Color.clear,0);
            var icon=JarVisUi.Icon(b.transform,"x",13,13,22);icon.color=Color.white;b.name="Close JarVis";
        }
        void BuildWidget()
        {
            widget=PrototypeUi.Panel(transform,"JarVis voice controls",16,72,300,142,dark,24,dark?"1a2636":"f4f8fd",dark?"35445b":"d7e5f3");widget.GetComponent<Image>().raycastTarget=true;
            var logo=JarVisUi.Rect(widget,"Listening JarVis",12,10,66,64).gameObject.AddComponent<Image>();logo.sprite=Resources.Load<Sprite>("Prototype/JarVisExpressionBase");logo.preserveAspect=true;logo.raycastTarget=false;
            eyeL=Eye(logo.transform,24,34);eyeR=Eye(logo.transform,33,34);
            motion=JarVisUi.Rect(widget,"Voice waveform",88,12,194,24).gameObject.AddComponent<RoompresentAssistantMotion>();motion.color=PrototypeUi.Hex(dark?"68e6bb":"217ba0");motion.raycastTarget=false;
            state=PrototypeUi.Text(widget,font,T("Hỏi JarVis về nhiệm vụ","Ask JarVis about this task"),88,42,194,36,14,dark,true);
            widgetActions=JarVisUi.Rect(widget,"Voice actions",12,82,276,48);
            var a=Action(widgetActions,"",0,0,52,OpenReport);a.name="Report incident";
            UniformUiSurface.Use(a.GetComponent<Image>(),16,PrototypeUi.Hex("fff0f2"),PrototypeUi.Hex("f6d9df"),1,false,false);
            Utility(a.transform,"alert-triangle",PrototypeUi.Hex("cd5364"));
            var chatButton=Action(widgetActions,"",76,0,52,OpenChat);chatButton.name="Chat with JarVis";Utility(chatButton.transform,"message-square",PrototypeUi.Ink(dark));
            mic=Action(widgetActions,"",152,0,52,ToggleVoice,true);mic.name="Speak to JarVis";
            micLabel=mic.GetComponentInChildren<TMP_Text>();micLabel.gameObject.SetActive(false);
            widgetMicIcon=JarVisUi.Rect(mic.transform,"Microphone / Send",13,11,26,26).gameObject.AddComponent<ChatActionIcon>();widgetMicIcon.Set(ChatActionIcon.Symbol.Microphone);widgetMicIcon.color=Color.white;widgetMicIcon.raycastTarget=false;
            RedClose(widgetActions,228,0,Close);LayoutWidget();
        }
        void Utility(Transform parent,string symbol,Color color)
        {
            var icon=JarVisUi.Rect(parent,symbol,13,11,26,26).gameObject.AddComponent<ImportedUtilityIcon>();icon.symbol=symbol;icon.color=color;icon.strokeWidth=1.9f;icon.raycastTarget=false;
        }
        void LayoutWidget()
        {
            if(widgetActions==null)return;
            float h=Mathf.Max(36,state.GetPreferredValues(state.text,194,0).y+2);
            state.rectTransform.sizeDelta=new Vector2(194,h);
            float row=Mathf.Max(82,42+h+8);widgetActions.anchoredPosition=new Vector2(12,-row);widget.sizeDelta=new Vector2(300,row+60);
        }
        RectTransform Eye(Transform parent,float x,float y)
        {
            var r=PrototypeUi.Panel(parent,"Eye",x,y,6,9,false,3,"effaff","00000000");return r;
        }
        TMP_InputField Field(Transform parent,string name,float x,float y,float width,float height,string placeholder,int limit,bool multi=false)
        {
            var field=PrototypeUi.Panel(parent,name,x,y,width,height,dark,12,dark?"223a54":"f1f6fc",dark?"476584":"dce5f0");field.GetComponent<Image>().raycastTarget=true;
            var view=JarVisUi.Rect(field,"Text viewport",10,8,width-20,height-16);view.gameObject.AddComponent<RectMask2D>();
            var text=PrototypeUi.Text(view,font,"",0,0,width-20,height-16,14,dark);
            var hint=PrototypeUi.Text(view,font,placeholder,0,0,width-20,height-16,13,dark,false,true);
            var f=field.gameObject.AddComponent<TMP_InputField>();f.textComponent=(TextMeshProUGUI)text;f.placeholder=hint;f.textViewport=view;f.characterLimit=limit;
            f.lineType=multi?TMP_InputField.LineType.MultiLineNewline:TMP_InputField.LineType.SingleLine;
            f.targetGraphic=field.GetComponent<Image>();f.caretColor=PrototypeUi.Ink(dark);f.customCaretColor=true;f.shouldHideMobileInput=true;return f;
        }
        void BuildChat()
        {
            chat=Card("JarVis task chat",390,216);
            var logo=JarVisUi.Rect(chat,"Speaking JarVis",12,8,42,40).gameObject.AddComponent<Image>();logo.sprite=Resources.Load<Sprite>("Prototype/JarVisExpressionBase");logo.preserveAspect=true;logo.raycastTarget=false;
            chatEyeL=Eye(logo.transform,15.3f,21.5f);chatEyeR=Eye(logo.transform,21,21.5f);chatEyeL.sizeDelta=chatEyeR.sizeDelta=new Vector2(4,6);
            PrototypeUi.Text(chat,font,"JarVis",62,15,76,26,18,dark,true);
            chatMotion=JarVisUi.Rect(chat,"Live voice waveform",140,16,156,26).gameObject.AddComponent<RoompresentAssistantMotion>();chatMotion.color=PrototypeUi.Hex(dark?"68e6bb":"217ba0");chatMotion.raycastTarget=false;
            RedClose(chat,314,8,Close);
            chatState=PrototypeUi.Text(chat,font,T("A1 → B · A2 → C · Nhắn tin hoặc nói","A1 → B · A2 → C · Text or voice"),16,46,346,34,11,dark,false,true);
            scroll=JarVisUi.Scroll(chat,12,84,354,230,out content);viewport=(RectTransform)scroll.transform;
            composer=JarVisUi.Rect(chat,"Message composer",12,326,354,52);
            input=Field(composer,"Message",0,0,210,48,T("Hỏi về nhiệm vụ…","Ask about your task…"),360);
            chatMicLabel=Action(composer,T("Nói","Voice"),218,0,62,ToggleVoice).GetComponentInChildren<TMP_Text>();
            send=Action(composer,T("Gửi","Send"),288,0,66,SendText,true);
            input.onSubmit.AddListener(_=>SendText());
            AddMessage(T("JarVis biết bạn đang giao A1 tới kệ trái B và A2 tới kệ phải C. Cần hỗ trợ, hãy nhắn hoặc nói. Khi mất tracking, bạn vẫn có thể xem sơ đồ 2D.","JarVis knows this task: A1 to left cart B, A2 to right cart C. Type or speak for help. A 2D schematic is available if tracking is lost."),false);
        }
        void BuildReport()
        {
            report=Card("Report incident popup",286,216);
            PrototypeUi.Text(report,font,T("Báo cáo sự cố","Report an incident"),16,15,270,28,18,dark,true);RedClose(report,314,8,Close);
            incident=Field(report,"Incident description",16,66,346,104,T("Mô tả sự cố bạn gặp…","Describe what happened…"),800,true);
            reportState=PrototypeUi.Text(report,font,T("Lưu trên máy kèm trạng thái nhiệm vụ. Chưa gửi quản lý.","Saved on this device with task status. Not sent to an administrator."),16,182,346,44,12,dark,false,true);
            Action(report,T("Lưu báo cáo trên máy","Save report on device"),16,228,346,SaveReport,true);
        }
        void BuildMap()
        {
            mapView=Card("Expanded 2D schematic",420,216);
            PrototypeUi.Text(mapView,font,T("Sơ đồ phòng","Room floorplan"),16,14,278,28,17,dark,true);RedClose(mapView,314,8,()=>mapView.gameObject.SetActive(false));
            mapFull=JarVisUi.Rect(mapView,"Map image",54,60,270,270).gameObject.AddComponent<RawImage>();mapFull.raycastTarget=false;
            var location=PrototypeUi.Text(mapView,font,"",16,342,346,60,13,dark,true);
            mapFull.gameObject.AddComponent<RoompresentMapPositionOverlay>().Configure(mapFull,location,dark,En,guide);
        }
        public void OpenWidget(){opened=true;HidePanels();LayoutWidget();widget.gameObject.SetActive(true);pill.gameObject.SetActive(false);widget.SetAsLastSibling();}
        public void OpenChat(){opened=true;HidePanels();chat.gameObject.SetActive(true);pill.gameObject.SetActive(true);chat.SetAsLastSibling();LayoutChat();}
        public void OpenReport(){Close();opened=true;report.gameObject.SetActive(true);pill.gameObject.SetActive(true);report.SetAsLastSibling();}
        void HidePanels(){input?.DeactivateInputField();incident?.DeactivateInputField();widget.gameObject.SetActive(false);chat.gameObject.SetActive(false);report.gameObject.SetActive(false);mapView.gameObject.SetActive(false);}
        public void Close()
        {
            generation++;permissionPending=busy=false;recorder.Cancel();speech?.Dispose();speech=null;StopAllCoroutines();StopSpeech();
            HidePanels();pill.gameObject.SetActive(true);opened=false;state.text=T("Hỏi JarVis về nhiệm vụ","Ask JarVis about this task");LayoutWidget();chatState.text=T("Đã dừng. Bạn có thể gửi câu hỏi mới.","Stopped. You can send a new question.");
        }
        public static string TaskContext(RoompresentDeliveryRecord task,string selected,bool tracking)
        {
            return "Roompresent 153613. DELIVERY trial: A1(book 4714551570610) -> B(left cart), A2(book 4714551570603) -> C(right cart). B="+(task.deliveredB?"done":"pending")+",C="+(task.deliveredC?"done":"pending")+". Selected="+selected+". Tracking="+(tracking?"valid":"LOST; position unknown")+". You advise only; cannot confirm delivery or change location. Verification requires destination QR then matching product barcode. A is fixed table. A local 2D schematic can be attached; it is not live localization.";
        }
        void SendText()
        {
            // The shared Send action must still submit a recording with an empty text field.
            if(recorder.State!=VoiceRecorderState.Idle){if(!busy&&!permissionPending)SendVoice();return;}
            string question=input.text.Trim();if(question.Length==0)return;
            if(!permissionPending&&recorder.State==VoiceRecorderState.Idle&&RoompresentMap2D.IsRequest(question,map!=null))
            {
                input.text="";input.DeactivateInputField();AddMessage(question,true);
                AddMap(JarvisChatLanguage.English(question,En));
                if(!busy)SetStatus(T("Đã gửi sơ đồ trên máy","Saved schematic sent"));return;
            }
            if(busy||permissionPending)return;
            if(!SessionReady()){if(WantsMap(question,guide.DeliveryTracking))AddMap();return;}input.text="";input.DeactivateInputField();AddMessage(question,true);busy=true;int ticket=++generation;StartCoroutine(Answer(question,false,ticket));
        }
        bool SessionReady(){if(OmsMobileSession.IsReady)return true;Error(T("Đăng nhập JarVis để dùng AI. Sơ đồ 2D vẫn xem được khi mất mạng.","Sign in to JarVis to use AI. The 2D schematic is available offline."));return false;}
        public void ToggleVoice()
        {
            if(busy||permissionPending)return;
            if(recorder.State!=VoiceRecorderState.Idle){SendVoice();return;}
            if(!SessionReady())return;StopSpeech();input.DeactivateInputField();incident.DeactivateInputField();
#if UNITY_ANDROID && !UNITY_EDITOR
            if(!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
            {
                permissionPending=true;int ticket=++generation;var callbacks=new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted+=_=>{if(ticket!=generation||this==null)return;permissionPending=false;BeginVoice();};
                callbacks.PermissionDenied+=_=>{if(ticket!=generation||this==null)return;permissionPending=false;Error(T("Cần cho phép micro để nói với JarVis.","Allow microphone access to speak to JarVis."));};
                callbacks.PermissionDeniedAndDontAskAgain+=_=>{if(ticket!=generation||this==null)return;permissionPending=false;Error(T("Bật quyền micro trong cài đặt ứng dụng.","Enable microphone access in app settings."));};
                UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone,callbacks);return;
            }
#endif
            BeginVoice();
        }
        void BeginVoice(){if(!isActiveAndEnabled)return;if(!widget.gameObject.activeSelf&&!chat.gameObject.activeSelf)OpenWidget();if(recorder.Begin())SetStatus(T("Đang nghe · bấm Gửi khi nói xong","Listening · tap Send when finished"));}
        void SendVoice()
        {
            var message=recorder.FinalizeMessage();if(message==null)return;
            byte[] bytes;try{bytes=File.ReadAllBytes(message.AudioPath);}catch(Exception){Error(T("Không đọc được bản ghi.","Couldn't read the recording."));return;}finally{message.Dispose();}
            busy=true;int ticket=++generation;StartCoroutine(Transcribe(bytes,ticket));
        }
        IEnumerator Transcribe(byte[] bytes,int ticket)
        {
            SetStatus(T("Đang chuyển giọng nói thành chữ…","Transcribing your voice…"));OmsMobileApi.Transcript transcript=null;string error=null;
            yield return OmsMobileApi.Transcribe(bytes,value=>transcript=value,value=>error=value);
            if(ticket!=generation)yield break;
            if(error!=null||string.IsNullOrWhiteSpace(transcript?.text)){busy=false;Error(error==null?T("Chưa nghe rõ. Hãy thử nói lại.","No clear speech. Try speaking again."):OmsMobileApi.FriendlyError(error,En));yield break;}
            OpenChat();AddMessage(transcript.text,true);yield return Answer(transcript.text,true,ticket);
        }
        public static bool WantsMap(string question,bool tracking)
        {
            string q=RoompresentMap2D.Normalize(question);return RoompresentMap2D.IsRequest(question)||!tracking&&(q.Contains("tracking")||q.Contains("mat")||q.Contains("lost")||q.Contains("khong thay duong")||q.Contains("where"));
        }
        IEnumerator Answer(string question,bool voice,int ticket)
        {
            bool attach=WantsMap(question,guide.DeliveryTracking);SetStatus(T("JarVis đang trả lời…","JarVis is thinking…"));
            string context=TaskContext(RoompresentDeliveryMission.Current,guide.SelectedStop,guide.DeliveryTracking);
            if(attach)context+=" Offer the local 2D schematic now; the app will attach it after speech.";
            OmsMobileApi.JarvisReply reply=null;string error=null;
            bool localMap=RoompresentMap2D.IsRequest(question,map!=null),english=JarvisChatLanguage.English(question,En);
            if(!localMap)yield return OmsMobileApi.AskJarvisInTask(question,context,previous,value=>reply=value,value=>error=value);
            else attach=true;
            if(ticket!=generation)yield break;
            if(!localMap&&(error!=null||string.IsNullOrWhiteSpace(reply?.message)))
            {busy=false;Error(error==null?T("Chưa có câu trả lời. Hãy gửi lại nếu cần.","No answer received. Send again if needed."):OmsMobileApi.FriendlyError(error,En));if(attach)AddMap();yield break;}
            string answer=localMap?(english?"Here is the saved Roompresent schematic. A is the table, B the left cart, C the right cart. This is a reference, not your live position.":"Mình gửi lại sơ đồ Roompresent đã lưu. A là bàn, B là kệ trái, C là kệ phải. Đây là sơ đồ tham khảo, không phải vị trí trực tiếp."):reply.message;previous=question+" / "+answer;
            if(previous.Length>180)previous=previous.Substring(0,180);
            OpenChat();AddMessage((!localMap&&reply.synthetic?T("Phản hồi dự phòng · ","Fallback reply · "):"")+answer,false);
            if(voice)
            {
                SetStatus(T("Đang chuẩn bị giọng đọc…","Preparing speech…"));AudioClip clip=null;speech=new OmsMobileApi.SpeechDownload();
                string spoken=answer.Length<=3500?answer:answer.Substring(0,3500);
                yield return OmsMobileApi.Speak(spoken,value=>clip=value,value=>error=value,speech);speech=null;
                if(ticket!=generation){if(clip!=null)Destroy(clip);yield break;}
                if(clip!=null)
                {speaker.clip=clip;speaker.Play();SetStatus(T("JarVis đang nói…","JarVis is speaking…"));while(speaker.isPlaying&&ticket==generation)yield return null;StopSpeech();}
                else AddMessage(T("Không phát được giọng đọc; câu trả lời vẫn ở trên.","Speech playback failed; the answer is above."),false);
            }
            if(ticket!=generation)yield break;
            // Attach only after the actual AudioSource has finished speaking.
            if(attach)AddMap(english);busy=false;SetStatus(T("Sẵn sàng · Nhắn tin hoặc nói","Ready · Type or speak"));
            Debug.Log("[RoompresentAssistant] Reply received; voice="+voice+" map="+attach+" synthetic="+(reply?.synthetic??false)+" localMap="+localMap+"; AR session retained.");
        }
        void SetStatus(string value){state.text=value;LayoutWidget();chatState.text=value;}
        void Error(string value){SetStatus(value);if(chat!=null&&chat.gameObject.activeSelf)AddMessage(value,false);}
        void AddMessage(string text,bool user)
        {
            if(content==null)return;
            // API and user content is plain text, never TMP markup.
            float width=322;var measured=font==null?Vector2.zero:chatState.GetPreferredValues(text,width-24,0);
            float height=Mathf.Max(44,measured.y+24);
            var bubble=PrototypeUi.Panel(content,user?"User message":"JarVis message",user?24:0,messageY,width,height,dark,14,user?"174b91":dark?"253b53":"f1f6fc","00000000");
            var label=PrototypeUi.Text(bubble,font,text,12,10,width-24,height-20,14,user||dark);label.overflowMode=TextOverflowModes.Overflow;
            // Use this label's exact font size when calculating final height.
            height=Mathf.Max(44,label.GetPreferredValues(text,width-24,0).y+24);bubble.sizeDelta=new Vector2(width,height);label.rectTransform.sizeDelta=new Vector2(width-24,height-20);
            messageY+=height+10;content.sizeDelta=new Vector2(354,Mathf.Max(viewport.rect.height,messageY));Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;
        }
        public void AddMap()=>AddMap(En);
        public void AddMap(bool english)
        {
            OpenChat();if(map==null)map=RoompresentMap2D.LoadReference()??RoompresentMap2D.Create(guide);
            if(map==null){AddMessage(T("Chưa có sơ đồ sàn để hiển thị.","No floor schematic is available."),false);return;}
            AddMessage((english?"2D schematic · A: table, B: left cart, C: right cart. Tap to enlarge. This is a reference, not live positioning.":"Sơ đồ 2D · A: bàn, B: kệ trái, C: kệ phải. Chạm ảnh để phóng to. Đây là sơ đồ tham khảo, không phải vị trí trực tiếp."),false);
            var r=PrototypeUi.Panel(content,"2D map attachment",0,messageY,188,188,dark,14);var image=JarVisUi.Rect(r,"Roompresent map",4,4,180,180).gameObject.AddComponent<RawImage>();image.texture=map;image.raycastTarget=false;
            PrototypeUi.Button(r,()=>{mapFull.texture=map;mapView.gameObject.SetActive(true);mapView.SetAsLastSibling();input.DeactivateInputField();});
            messageY+=198;content.sizeDelta=new Vector2(354,Mathf.Max(viewport.rect.height,messageY));Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=0;
        }
        [Serializable] sealed class Incident {public string id,createdUtc,description,taskContext,status="LOCAL_PENDING_NOT_SENT";}
        void SaveReport()
        {
            string text=incident.text.Trim();if(text.Length==0){reportState.text=T("Nhập mô tả trước khi lưu.","Enter a description before saving.");return;}
            try
            {
                var data=new Incident{id=Guid.NewGuid().ToString("N"),createdUtc=DateTime.UtcNow.ToString("O"),description=text,taskContext=TaskContext(RoompresentDeliveryMission.Current,guide.SelectedStop,guide.DeliveryTracking)};
                string folder=Path.Combine(Application.persistentDataPath,"JarVisIncidents");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,data.id+".json"),JsonUtility.ToJson(data,true));
                incident.text="";incident.DeactivateInputField();reportState.text=T("Đã lưu trên máy. Báo cáo chưa được gửi quản lý.","Saved on device. This report has not been sent to an administrator.");
            }
            catch(Exception){reportState.text=T("Chưa lưu được. Hãy giữ lại mô tả và thử lại.","Couldn't save. Keep your description and try again.");}
        }
        void LayoutChat()
        {
            float keyboard=TouchScreenKeyboard.visible?TouchScreenKeyboard.area.height/Mathf.Max(1,Screen.height)*safe.rect.height:0;
            float bottom=keyboard>0?keyboard+8:216;
            float height=Mathf.Clamp(safe.rect.height-bottom-140,200,390);
            float normalized=scroll.verticalNormalizedPosition;
            bool changed=Mathf.Abs(chat.sizeDelta.y-height)>.5f||Mathf.Abs(content.sizeDelta.y-Mathf.Max(height-160,messageY))>.5f;
            chat.sizeDelta=new Vector2(378,height);chat.anchoredPosition=new Vector2(0,bottom);
            viewport.sizeDelta=new Vector2(354,height-160);composer.anchoredPosition=new Vector2(12,-height+64);
            content.sizeDelta=new Vector2(354,Mathf.Max(height-160,messageY));
            if(changed){Canvas.ForceUpdateCanvases();scroll.verticalNormalizedPosition=normalized<.1f?0:normalized;}
            if(report.gameObject.activeSelf)report.anchoredPosition=new Vector2(0,keyboard>0?keyboard+8:216);
        }
        void Update()
        {
            if(guide==null)return;
            if(chat.gameObject.activeSelf||report.gameObject.activeSelf)LayoutChat();
            bool recording=recorder.State==VoiceRecorderState.Recording||recorder.State==VoiceRecorderState.Stopped;
            micLabel.text=recording?T("Gửi giọng","Send voice"):T("Nói","Speak");mic.interactable=!busy&&!permissionPending;
            widgetMicIcon.Set(recording?ChatActionIcon.Symbol.Send:ChatActionIcon.Symbol.Microphone);
            chatMicLabel.text=recording?T("Gửi","Send"):T("Nói","Voice");
            send.interactable=!permissionPending&&(!busy||RoompresentMap2D.IsRequest(input.text,map!=null));
            if(recording)SetStatus((recorder.State==VoiceRecorderState.Stopped?T("Đã dừng · ","Stopped · "):T("Đang nghe · ","Listening · "))+Mathf.FloorToInt(recorder.Duration)+"s / 30s · "+T("Gửi hoặc × hủy","Send or × cancel"));
            if(speaker.isPlaying)
            {
                speaker.GetOutputData(samples,0);double energy=0;for(int i=0;i<samples.Length;i++)energy+=samples[i]*samples[i];
                Array.Copy(outputLevels,1,outputLevels,0,outputLevels.Length-1);outputLevels[outputLevels.Length-1]=Mathf.Clamp01((float)Math.Sqrt(energy/samples.Length)*4);motion.Levels=outputLevels;
            }
            else if(recording)motion.Levels=recorder.Levels;
            else {Array.Clear(outputLevels,0,outputLevels.Length);motion.Levels=outputLevels;}
            chatMotion.Levels=motion.Levels;
            float blink=PrototypeUi.ReducedMotion?1:Time.unscaledTime%5.8f>5.65f?.15f:1;
            Vector2 offset=PrototypeUi.ReducedMotion?Vector2.zero:motion.EyeOffset;
            eyeL.anchoredPosition=new Vector2(24,-34)+offset;eyeR.anchoredPosition=new Vector2(33,-34)+offset;eyeL.localScale=eyeR.localScale=new Vector3(1,blink,1);
            Vector2 chatOffset=PrototypeUi.ReducedMotion?Vector2.zero:chatMotion.EyeOffset*.65f;
            chatEyeL.anchoredPosition=new Vector2(15.3f,-21.5f)+chatOffset;chatEyeR.anchoredPosition=new Vector2(21,-21.5f)+chatOffset;chatEyeL.localScale=chatEyeR.localScale=new Vector3(1,blink,1);
            // Code verification owns the camera frame; retain messages but collapse the assistant.
            bool verifying=guide.Verification.Active;
            if(opened&&verifying&&!wasVerifying&&!MediaBusy&&!input.isFocused&&!incident.isFocused){HidePanels();pill.gameObject.SetActive(true);opened=false;}
            wasVerifying=verifying;
        }
        void StopSpeech(){speaker.Stop();if(speaker.clip!=null){Destroy(speaker.clip);speaker.clip=null;}}
        void OnApplicationPause(bool paused){if(paused&&!permissionPending&&recorder!=null)Close();}
        void OnDestroy(){generation++;speech?.Dispose();if(recorder!=null)recorder.Cancel();if(speaker!=null)StopSpeech();if(map!=null)Destroy(map);}
    }
}
