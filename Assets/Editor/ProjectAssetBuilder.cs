using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ProjectAssetBuilder
{
    const string Root = "Assets/Game";
    static Sprite square, blade, circle, icon, guardSprite, gripSprite;
    static Material material;
    static Font font;
    static SwordDefinition[] definitions;
    static SwordView sword;
    static PlayerController player;
    static EnemyView enemy;
    static BulletView bullet;
    static HitEffectView effect;
    static SwordSlot slot;
    static UIManager ui;
    static GameObject terrainBlock;
    static Color Ink = new Color32(16, 24, 37, 255);
    static Color Panel = new Color32(25, 40, 56, 255);
    static Color Mint = new Color32(113, 248, 225, 255);

    [MenuItem("Flying Sword/Build Project Assets")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before building assets.");
        Directory.CreateDirectory(Root + "/Art");
        Directory.CreateDirectory(Root + "/Prefabs");
        Directory.CreateDirectory(Root + "/Equipment");
        Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.Refresh();
        font = GameFonts.Body;
        square = SpriteAsset("Square", (x,y) => Color.white);
        blade = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/Blade.png");
        if (!blade) blade = SpriteAsset("Blade", (x,y) => Mathf.Abs(x-.5f) < .34f * Mathf.Min(1,y/.26f) ? Color.white : Color.clear);
        guardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/SwordGuard.png");
        gripSprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/SwordGrip.png");
        circle = SpriteAsset("Range", (x,y) => Mathf.Abs(new Vector2(x-.5f,y-.5f).magnitude-.47f)<.015f ? Color.white : Color.clear);
        icon = SpriteAsset("SwordIcon", (x,y) => (y>.28f && y<.93f && Mathf.Abs(x-.5f)<.095f*(.96f-y)/.68f) || (y>.24f && y<.30f && x>.25f && x<.75f) || (y>.08f && y<.25f && Mathf.Abs(x-.5f)<.045f) ? Color.white : Color.clear);
        material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/SwordTrail.mat");
        if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")); AssetDatabase.CreateAsset(material, Root + "/Art/SwordTrail.mat"); }
        BuildDefinitions();
        var block=Sprite("Terrain block",null,Vector2.zero,Vector2.one,new Color32(42,59,73,255),null,-2);block.gameObject.AddComponent<BoxCollider2D>();Sprite("Top edge",block.transform,new Vector2(0,.48f),new Vector2(1,.04f),new Color32(75,111,122,255),null,-1);terrainBlock=PrefabUtility.SaveAsPrefabAsset(block.gameObject,Root+"/Prefabs/TerrainBlock.prefab");UnityEngine.Object.DestroyImmediate(block.gameObject);
        sword = BuildSword(); player = BuildPlayer(); enemy = BuildEnemy(); bullet = BuildBullet(); effect = BuildEffect(); slot = BuildSlot(); ui = BuildUI();
        BuildStage("Combat", false); BuildStage("Metroidvania", true); BuildTitle();
        var legacy = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var router = new GameObject("Legacy scene redirect").AddComponent<SceneRouter>(); Set(router,"redirectToTitle",true);
        EditorSceneManager.SaveScene(legacy,"Assets/Scenes/SampleScene.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Title.unity",true),new EditorBuildSettingsScene("Assets/Scenes/Combat.unity",true),new EditorBuildSettingsScene("Assets/Scenes/Metroidvania.unity",true),new EditorBuildSettingsScene("Assets/Scenes/Tutorial.unity",true) };
        AssetDatabase.SaveAssets();
        StageExpansion.Apply();
        BossStageSetup.Apply();
        EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");
        Debug.Log("Flying Sword assets built: 3 scenes, gameplay prefabs, Canvas UI and 4 sword definitions.");
    }

    static Sprite SpriteAsset(string name, Func<float,float,Color> pixel)
    {
        string path=Root+"/Art/"+name+".png";
        var texture=new Texture2D(128,128,TextureFormat.RGBA32,false);
        for(int y=0;y<128;y++) for(int x=0;x<128;x++) texture.SetPixel(x,y,pixel((x+.5f)/128,(y+.5f)/128));
        texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spritePixelsPerUnit=128; importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    static void Set(UnityEngine.Object target,string field,object value)
    {
        var serialized=new SerializedObject(target); var p=serialized.FindProperty(field);
        if(p==null) throw new InvalidOperationException(target.GetType().Name+"."+field);
        if(value is UnityEngine.Object obj) p.objectReferenceValue=obj;
        else if(value is bool b) p.boolValue=b;
        else if(value is UnityEngine.Object[] array) { p.arraySize=array.Length; for(int i=0;i<array.Length;i++)p.GetArrayElementAtIndex(i).objectReferenceValue=array[i]; }
        else throw new InvalidOperationException("Unsupported field "+field);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static T Save<T>(GameObject root,string name) where T:Component
    {
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/"+name+".prefab"); UnityEngine.Object.DestroyImmediate(root); return prefab.GetComponent<T>();
    }
    static SpriteRenderer Sprite(string name,Transform parent,Vector2 position,Vector2 scale,Color color,Sprite art=null,int order=0)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=new Vector3(scale.x,scale.y,1);
        var r=go.AddComponent<SpriteRenderer>();r.sprite=art?art:square;r.color=color;r.sortingOrder=order;return r;
    }
    static TrailRenderer Trail(Transform parent,float width)
    {
        var go=new GameObject("Trail");go.transform.SetParent(parent,false);go.transform.localPosition=new Vector3(0,.50f,0);var t=go.AddComponent<TrailRenderer>();t.sharedMaterial=material;t.time=.20f;t.startWidth=width;t.endWidth=0;t.startColor=new Color(.38f,1,.92f,.65f);t.endColor=new Color(.38f,1,.92f,0);t.minVertexDistance=.035f;t.numCornerVertices=3;t.numCapVertices=3;t.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;t.receiveShadows=false;t.sortingOrder=3;t.emitting=false;return t;
    }
    static void BuildDefinitions()
    {
        string[] names={"기본 검","질풍검","귀환검","낙인검"}; string[] descriptions={"기본 공격력 1","전체 검 속도 +15% · 중첩","회수 속도 +25% · 중첩","명중한 적을 추적 대상으로 지정"};
        Color[] colors={Mint,new Color32(143,202,255,255),new Color32(187,155,255,255),new Color32(255,182,112,255)};
        definitions=new SwordDefinition[4];
        for(int i=0;i<4;i++) { string path=Root+"/Equipment/"+((SwordKind)i)+".asset";var d=AssetDatabase.LoadAssetAtPath<SwordDefinition>(path);if(!d){d=ScriptableObject.CreateInstance<SwordDefinition>();AssetDatabase.CreateAsset(d,path);} d.kind=(SwordKind)i;d.displayName=names[i];d.description=descriptions[i];d.icon=icon;d.bladeColor=colors[i];d.damage=1;d.speedBonus=i==1?.15f:0;d.recallBonus=i==2?.25f:0;d.marksTarget=i==3;d.startingCount=i==0?20:2;d.initiallyEquipped=i==0;EditorUtility.SetDirty(d);definitions[i]=d; }
    }
    static SwordView BuildSword()
    {
        var go=new GameObject("Sword");var v=go.AddComponent<SwordView>();var visual=new GameObject("Visual").transform;visual.SetParent(go.transform,false);v.visual=visual;
        v.blade=Sprite("Blade",visual,new Vector2(0,.23f),new Vector2(.18f,.46f),Mint,blade,5);
        Sprite("Guard",visual,new Vector2(0,.46f),new Vector2(.22f,.055f),new Color32(244,197,112,255),guardSprite,6);
        Sprite("Grip",visual,new Vector2(0,.54f),new Vector2(.055f,.15f),new Color32(98,110,138,255),gripSprite,6);
        v.groundMark=Sprite("Ground mark",go.transform,Vector2.zero,new Vector2(.35f,.055f),Mint,null,2);v.groundMark.enabled=false;
        v.trail=Trail(visual,.065f);
        var canvas=WorldCanvas(go.transform,new Vector2(0,.9f));v.fusionCount=Label("Fusion count",canvas,"",28,TextAnchor.MiddleCenter);Fill(v.fusionCount.rectTransform);v.fusionCount.gameObject.SetActive(false);
        return Save<SwordView>(go,"Sword");
    }
    static PlayerController BuildPlayer()
    {
        var go=new GameObject("Player");var controller=go.AddComponent<PlayerController>();
        var hero=Sprite("Rider",go.transform,new Vector2(0,.12f),new Vector2(.27f,.46f),new Color32(245,238,218,255),null,8);
        var mount=UnityEngine.Object.Instantiate(sword,go.transform);mount.name="Riding sword";mount.transform.localPosition=new Vector2(.75f,-.17f);mount.transform.localRotation=Quaternion.Euler(0,0,90);mount.transform.localScale=new Vector3(1.25f,2.3f,1);mount.trail.time=.28f;mount.trail.startWidth=.11f;
        Set(controller,"hero",hero);Set(controller,"mountTrail",mount.trail);return Save<PlayerController>(go,"Player");
    }
    static Transform WorldCanvas(Transform parent,Vector2 position)
    {
        var go=new GameObject("World UI",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=Vector3.one*.01f;var c=go.GetComponent<Canvas>();c.renderMode=RenderMode.WorldSpace;c.sortingOrder=12;go.GetComponent<RectTransform>().sizeDelta=new Vector2(180,90);return go.transform;
    }
    static LineRenderer Line(string name,Transform parent,bool world,float width)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=world;line.startWidth=line.endWidth=width;line.startColor=line.endColor=new Color(.45f,.7f,1,.16f);line.sortingOrder=-1;return line;
    }
    static EnemyView BuildEnemy()
    {
        var go=new GameObject("Enemy");var v=go.AddComponent<EnemyView>();v.body=Sprite("Body",go.transform,Vector2.zero,Vector2.one*.8f,Color.red,null,4);v.core=Sprite("Core",go.transform,Vector2.zero,Vector2.one*.29f,Color.white,null,5);v.core.transform.localRotation=Quaternion.Euler(0,0,45);
        var canvas=WorldCanvas(go.transform,new Vector2(0,1.2f));var hp=Label("Health",canvas,"HP 30/30",22,TextAnchor.MiddleCenter);Place(hp.rectTransform,new Vector2(0,23),new Vector2(180,30));
        var health=Bar(canvas,"Health bar",new Vector2(0,0),new Vector2(160,8),Mint);var charge=Bar(canvas,"Special charge",new Vector2(0,-14),new Vector2(160,6),new Color32(240,169,97,255));var status=Label("Special status",canvas,"특공 5.0초",18,TextAnchor.MiddleCenter);Place(status.rectTransform,new Vector2(0,-34),new Vector2(180,26));
        var mark=Sprite("Target mark",go.transform,new Vector2(0,.7f),Vector2.one*.35f,new Color32(255,182,112,255),circle,9).gameObject;mark.SetActive(false);
        var range=Line("Detection range",go.transform,false,.025f);range.loop=true;range.positionCount=96;
        var sight=Line("Sight line",go.transform,true,.02f);sight.positionCount=2;
        Set(v,"healthText",hp);Set(v,"healthFill",health);Set(v,"burstFill",charge);Set(v,"statusText",status);Set(v,"lockMarker",mark);Set(v,"detectionCircle",range);Set(v,"sightLine",sight);
        return Save<EnemyView>(go,"Enemy");
    }
    static BulletView BuildBullet() {var go=new GameObject("Enemy bullet");var v=go.AddComponent<BulletView>();Sprite("Visual",go.transform,Vector2.zero,Vector2.one*.19f,new Color32(255,111,94,255),null,5).transform.localRotation=Quaternion.Euler(0,0,45);return Save<BulletView>(go,"EnemyBullet");}
    static HitEffectView BuildEffect() {var go=new GameObject("Hit effect");var v=go.AddComponent<HitEffectView>();v.sparks=new SpriteRenderer[8];for(int i=0;i<8;i++){v.sparks[i]=Sprite("Spark "+i,go.transform,Vector2.zero,new Vector2(.18f,.055f),Mint,null,9);v.sparks[i].enabled=false;}return Save<HitEffectView>(go,"HitEffect");}

    static RectTransform Rect(string name,Transform parent) {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);return go.GetComponent<RectTransform>();}
    static void Fill(RectTransform rect,float margin=0) {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*margin;rect.offsetMax=-Vector2.one*margin;}
    static void Place(RectTransform rect,Vector2 position,Vector2 size) {rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;}
    static Image Image(string name,Transform parent,Color color) {var rect=Rect(name,parent);var image=rect.gameObject.AddComponent<Image>();image.color=color;return image;}
    static Text Label(string name,Transform parent,string value,int size,TextAnchor alignment=TextAnchor.MiddleLeft) {var rect=Rect(name,parent);var text=rect.gameObject.AddComponent<Text>();text.font=font;text.fontSize=size;text.text=value;text.color=new Color32(231,242,246,255);text.alignment=alignment;text.raycastTarget=false;return text;}
    static Button Button(string name,Transform parent,string text,Vector2 position,Vector2 size,UnityAction action) {var bg=Image(name,parent,new Color32(42,77,91,255));Place(bg.rectTransform,position,size);var button=bg.gameObject.AddComponent<Button>();button.targetGraphic=bg;var colors=button.colors;colors.highlightedColor=new Color(.7f,1,.93f);colors.pressedColor=new Color(.4f,.8f,.7f);button.colors=colors;var label=Label("Label",bg.transform,text,25,TextAnchor.MiddleCenter);Fill(label.rectTransform,8);UnityEventTools.AddPersistentListener(button.onClick,action);return button;}
    static Image Bar(Transform parent,string name,Vector2 position,Vector2 size,Color color) {var bg=Image(name,parent,new Color32(38,51,65,255));Place(bg.rectTransform,position,size);var fill=Image("Fill",bg.transform,color);Fill(fill.rectTransform);fill.sprite=square;fill.type=UnityEngine.UI.Image.Type.Filled;fill.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal;return fill;}
    static SwordSlot BuildSlot()
    {
        var bg=Image("Sword slot",null,new Color(.1f,.17f,.24f));bg.rectTransform.sizeDelta=new Vector2(145,132);var card=bg.gameObject.AddComponent<SwordSlot>();bg.gameObject.AddComponent<CanvasGroup>();
        var accent=Image("Type accent",bg.transform,Mint);accent.rectTransform.anchorMin=new Vector2(0,1);accent.rectTransform.anchorMax=Vector2.one;accent.rectTransform.pivot=new Vector2(.5f,1);accent.rectTransform.sizeDelta=new Vector2(0,4);
        var artwork=Image("Sword icon",bg.transform,Mint);artwork.sprite=icon;artwork.preserveAspect=true;Place(artwork.rectTransform,new Vector2(-41,15),new Vector2(46,70));artwork.raycastTarget=false;
        var name=Label("Name",bg.transform,"질풍검 01",18,TextAnchor.MiddleLeft);Place(name.rectTransform,new Vector2(23,24),new Vector2(80,42));
        var desc=Label("Effect",bg.transform,"전체 검 속도 +15% · 중첩",15,TextAnchor.MiddleLeft);Place(desc.rectTransform,new Vector2(0,-36),new Vector2(125,44));
        Set(card,"background",bg);Set(card,"icon",artwork);Set(card,"accent",accent);Set(card,"nameLabel",name);Set(card,"effectLabel",desc);return Save<SwordSlot>(bg.gameObject,"SwordSlot");
    }
    static GameObject Overlay(string name,Transform parent) {var bg=Image(name,parent,new Color(0.025f,.045f,.07f,.95f));Fill(bg.rectTransform);return bg.gameObject;}
    static UIManager BuildUI()
    {
        var root=new GameObject("Game UI",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(UIManager));var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;var manager=root.GetComponent<UIManager>();
        var events=new GameObject("UI Input",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(root.transform,false);
        var hud=Rect("HUD",root.transform);Fill(hud);
        var hp=Label("Player health",hud,"HP 10/10",30);hp.rectTransform.anchorMin=hp.rectTransform.anchorMax=new Vector2(0,1);hp.rectTransform.pivot=new Vector2(0,1);hp.rectTransform.anchoredPosition=new Vector2(36,-28);hp.rectTransform.sizeDelta=new Vector2(300,42);
        var fill=Bar(hp.transform,"Health bar",new Vector2(0,-33),new Vector2(250,9),Mint);
        var count=Label("Sword count",hud,"사용 가능 검",23);count.rectTransform.anchorMin=count.rectTransform.anchorMax=new Vector2(0,1);count.rectTransform.pivot=new Vector2(0,1);count.rectTransform.anchoredPosition=new Vector2(36,-95);count.rectTransform.sizeDelta=new Vector2(350,34);
        var wave=Label("Wave timer",hud,"",26,TextAnchor.MiddleCenter);wave.rectTransform.anchorMin=wave.rectTransform.anchorMax=new Vector2(.5f,1);wave.rectTransform.anchoredPosition=new Vector2(0,-48);wave.rectTransform.sizeDelta=new Vector2(650,45);
        var bag=Button("Open inventory",hud,"검 장비  [Tab]",Vector2.zero,new Vector2(220,64),manager.OpenInventory).GetComponent<RectTransform>();bag.anchorMin=bag.anchorMax=new Vector2(1,1);bag.pivot=new Vector2(1,1);bag.anchoredPosition=new Vector2(-36,-28);
        var title=Overlay("Title",root.transform);var titleText=Label("Game title",title.transform,"FLYING SWORD",66,TextAnchor.MiddleCenter);Place(titleText.rectTransform,new Vector2(0,270),new Vector2(1000,100));titleText.color=Mint;var subtitle=Label("Subtitle",title.transform,"검을 타고, 궤적을 그리다",29,TextAnchor.MiddleCenter);Place(subtitle.rectTransform,new Vector2(0,175),new Vector2(900,60));
        Button("Tutorial stage",title.transform,"튜토리얼",new Vector2(0,80),new Vector2(560,100),manager.StartTutorial);Button("Metroidvania stage",title.transform,"메트로배니아",new Vector2(0,-100),new Vector2(560,100),manager.StartMetroidvania).gameObject.SetActive(false);
        Button("Boss stage", title.transform, "보스전", new Vector2(0, -50), new Vector2(560, 100), manager.StartBoss);
        Button("Boss challenge", title.transform, "보스전 2", new Vector2(0, -180), new Vector2(560, 100), manager.StartBossChallenge);
        var hint=Label("Stage hint",title.transform,"스테이지 선택  ·  Esc로 일시 정지 및 타이틀 이동",22,TextAnchor.MiddleCenter);Place(hint.rectTransform,new Vector2(0,-330),new Vector2(1000,60));
        var pause=Overlay("Pause",root.transform);var ptitle=Label("Heading",pause.transform,"잠시 멈춤",46,TextAnchor.MiddleCenter);Place(ptitle.rectTransform,new Vector2(0,190),new Vector2(700,80));Button("Continue",pause.transform,"계속하기",new Vector2(0,40),new Vector2(500,90),manager.Resume);Button("Return to title",pause.transform,"타이틀로 돌아가기",new Vector2(0,-80),new Vector2(500,90),manager.ReturnToTitle);
        var defeat=Overlay("Defeat",root.transform);var dtitle=Label("Heading",defeat.transform,"검의 궤적이 멈췄습니다",44,TextAnchor.MiddleCenter);Place(dtitle.rectTransform,new Vector2(0,190),new Vector2(900,80));Button("Restart",defeat.transform,"다시 도전",new Vector2(0,40),new Vector2(500,90),manager.RestartStage);Button("Return to title",defeat.transform,"타이틀로 돌아가기",new Vector2(0,-80),new Vector2(500,90),manager.ReturnToTitle);
        var inventory=Overlay("Inventory",root.transform).AddComponent<InventoryPanel>();var panel=Image("Equipment panel",inventory.transform,Ink);Fill(panel.rectTransform);panel.rectTransform.anchorMin=new Vector2(.055f,.065f);panel.rectTransform.anchorMax=new Vector2(.945f,.935f);panel.rectTransform.offsetMin=panel.rectTransform.offsetMax=Vector2.zero;
        var heading=Label("Heading",panel.transform,"검 장비",42);heading.rectTransform.anchorMin=heading.rectTransform.anchorMax=new Vector2(0,1);heading.rectTransform.pivot=new Vector2(0,1);heading.rectTransform.anchoredPosition=new Vector2(32,-20);heading.rectTransform.sizeDelta=new Vector2(700,60);
        var help=Label("Instructions",panel.transform,"카드를 클릭하거나 반대편으로 드래그해 장착을 바꾸세요",22);help.rectTransform.anchorMin=help.rectTransform.anchorMax=new Vector2(0,1);help.rectTransform.pivot=new Vector2(0,1);help.rectTransform.anchoredPosition=new Vector2(34,-86);help.rectTransform.sizeDelta=new Vector2(1100,40);
        var close=Button("Close",panel.transform,"닫기  [Tab]",Vector2.zero,new Vector2(180,60),manager.Resume).GetComponent<RectTransform>();close.anchorMin=close.anchorMax=Vector2.one;close.pivot=Vector2.one;close.anchoredPosition=new Vector2(-28,-24);
        var equipped=Section(panel.transform,"Equipped",new Vector2(.02f,.14f),new Vector2(.49f,.84f),inventory.StoreAll,"모두 보관");var storage=Section(panel.transform,"Storage",new Vector2(.51f,.14f),new Vector2(.98f,.84f),inventory.EquipAll,"모두 장착");
        var summary=Label("Equipment summary",panel.transform,"",24,TextAnchor.MiddleCenter);summary.rectTransform.anchorMin=new Vector2(0,0);summary.rectTransform.anchorMax=new Vector2(1,.12f);summary.rectTransform.offsetMin=new Vector2(24,8);summary.rectTransform.offsetMax=new Vector2(-24,-8);
        var drag=Rect("Drag layer",inventory.transform);Fill(drag);
        Set(inventory,"equippedSection",equipped);Set(inventory,"storageSection",storage);Set(inventory,"slotPrefab",slot);Set(inventory,"dragLayer",drag);Set(inventory,"summary",summary);
        Set(manager,"titlePanel",title);Set(manager,"hud",hud.gameObject);Set(manager,"pausePanel",pause);Set(manager,"defeatPanel",defeat);Set(manager,"inventory",inventory);Set(manager,"playerHealth",hp);Set(manager,"playerHealthFill",fill);Set(manager,"swordCount",count);Set(manager,"waveTimer",wave);
        title.SetActive(false);pause.SetActive(false);defeat.SetActive(false);inventory.gameObject.SetActive(false);
        return Save<UIManager>(root,"GameUI");
    }
    static InventorySection Section(Transform parent,string name,Vector2 min,Vector2 max,UnityAction bulk,string caption)
    {
        var bg=Image(name,parent,Panel);bg.rectTransform.anchorMin=min;bg.rectTransform.anchorMax=max;bg.rectTransform.offsetMin=bg.rectTransform.offsetMax=Vector2.zero;var section=bg.gameObject.AddComponent<InventorySection>();
        var heading=Label("Section heading",bg.transform,name,29);heading.rectTransform.anchorMin=new Vector2(0,1);heading.rectTransform.anchorMax=new Vector2(1,1);heading.rectTransform.pivot=new Vector2(.5f,1);heading.rectTransform.offsetMin=new Vector2(24,-76);heading.rectTransform.offsetMax=new Vector2(-210,-10);
        var button=Button("Bulk action",bg.transform,caption,Vector2.zero,new Vector2(170,52),bulk).GetComponent<RectTransform>();button.anchorMin=button.anchorMax=Vector2.one;button.pivot=Vector2.one;button.anchoredPosition=new Vector2(-18,-18);
        var viewport=Image("Viewport",bg.transform,new Color(1,1,1,.01f));Fill(viewport.rectTransform,16);viewport.rectTransform.offsetMax=new Vector2(-16,-84);viewport.gameObject.AddComponent<RectMask2D>();
        var content=Rect("Cards",viewport.transform);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;content.anchoredPosition=Vector2.zero;
        var grid=content.gameObject.AddComponent<GridLayoutGroup>();grid.spacing=new Vector2(12,12);grid.padding=new RectOffset(2,2,2,12);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=5;grid.cellSize=new Vector2(140,132);content.gameObject.AddComponent<ResponsiveInventoryGrid>();var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll=bg.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport.rectTransform;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;
        Set(section,"content",content);Set(section,"heading",heading);Set(section,"background",bg);return section;
    }
    static Camera Camera() {var go=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));go.tag="MainCamera";go.transform.position=new Vector3(0,0,-10);var camera=go.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=7.16f;camera.backgroundColor=Ink;camera.clearFlags=CameraClearFlags.SolidColor;return camera;}
    static void BuildTitle() {var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);Camera();var instance=(UIManager)PrefabUtility.InstantiatePrefab(ui);Set(instance,"titleScreen",true);instance.transform.Find("Title").gameObject.SetActive(true);instance.transform.Find("HUD").gameObject.SetActive(false);EditorSceneManager.SaveScene(scene,"Assets/Scenes/Title.unity");}
    static void BuildStage(string name,bool metro)
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var camera=Camera();var follow=camera.gameObject.AddComponent<CameraFollow>();
        var terrain=new GameObject("Stage Geometry");var geometry=terrain.AddComponent<StageGeometry>();var surfaces=new List<BoxCollider2D>();
        Surface(terrain.transform,surfaces,"Floor",new Vector2(0,-7.25f),new Vector2(45,1));Surface(terrain.transform,surfaces,"Ceiling",new Vector2(0,7.25f),new Vector2(45,1));Surface(terrain.transform,surfaces,"West wall",new Vector2(-22.5f,0),new Vector2(1,15));Surface(terrain.transform,surfaces,"East wall",new Vector2(22.5f,0),new Vector2(1,15));
        if(metro) {Surface(terrain.transform,surfaces,"West lower ledge",new Vector2(-15,-3.8f),new Vector2(7,1));Surface(terrain.transform,surfaces,"West upper ledge",new Vector2(-12,2.8f),new Vector2(5,1));Surface(terrain.transform,surfaces,"Central pillar",new Vector2(-4,-3.5f),new Vector2(2,5.5f));Surface(terrain.transform,surfaces,"Central bridge",new Vector2(2,.8f),new Vector2(7,.8f));Surface(terrain.transform,surfaces,"Upper ledge",new Vector2(7,4.5f),new Vector2(6,.8f));Surface(terrain.transform,surfaces,"East pillar",new Vector2(13,-3.7f),new Vector2(2,5));Surface(terrain.transform,surfaces,"East ledge",new Vector2(17,1.5f),new Vector2(5,1));Surface(terrain.transform,surfaces,"Lower passage",new Vector2(4,-4.7f),new Vector2(6,.8f));}
        Set(geometry,"surfaces",surfaces.ToArray());
        var hero=(PlayerController)PrefabUtility.InstantiatePrefab(player);hero.transform.position=new Vector2(-12,0);Set(follow,"view",camera);Set(follow,"target",hero.transform);
        var systems=new GameObject("Gameplay Systems");var stage=systems.AddComponent<StageManager>();var equipment=systems.AddComponent<EquipmentSystem>();var enemies=systems.AddComponent<EnemySystem>();var swords=systems.AddComponent<SwordSystem>();Set(equipment,"definitions",definitions);Set(enemies,"enemyPrefab",enemy);Set(enemies,"bulletPrefab",bullet);Set(swords,"swordPrefab",sword);Set(swords,"hitEffectPrefab",effect);
        var recall=Sprite("Recall range",systems.transform,Vector2.zero,Vector2.one,new Color(.4f,1,.9f,.4f),circle,2);recall.enabled=false;var guard=Sprite("Guard range",systems.transform,Vector2.zero,Vector2.one,new Color(.4f,1,.9f,.2f),circle,2);guard.enabled=false;Set(swords,"recallArea",recall);Set(swords,"guardArea",guard);
        var screen=(UIManager)PrefabUtility.InstantiatePrefab(ui);Set(stage,"geometry",geometry);Set(stage,"cameraFollow",follow);Set(stage,"player",hero);Set(stage,"swordSystem",swords);Set(stage,"enemySystem",enemies);Set(stage,"equipment",equipment);Set(stage,"ui",screen);
        EditorSceneManager.SaveScene(scene,"Assets/Scenes/"+name+".unity");
    }
    static void Surface(Transform parent,List<BoxCollider2D> surfaces,string name,Vector2 position,Vector2 size)
    {
        var body=(GameObject)PrefabUtility.InstantiatePrefab(terrainBlock,parent);body.name=name;body.transform.localPosition=position;body.transform.localScale=new Vector3(size.x,size.y,1);surfaces.Add(body.GetComponent<BoxCollider2D>());
    }
}
