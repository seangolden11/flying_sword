using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

// Runs against real authored scenes and prefabs, then discards the test scene.
public static class GameRegressionChecks
{
    static int assertions;
    static object Call(object target,string method,params object[] args) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic|BindingFlags.Public).Invoke(target,args);
    static T Field<T>(object target,string field) => (T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(target);
    static void Check(bool value,string message) { assertions++;if(!value)throw new Exception(message); }
    [MenuItem("Flying Sword/Validate Project")]
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play before validation.");
        assertions=0;string result="";
        try
        {
            foreach(string sceneName in new[]{"Combat","Metroidvania"})
            {
                EditorSceneManager.OpenScene("Assets/Scenes/"+sceneName+".unity");
                Physics2D.SyncTransforms();
                var stage=UnityEngine.Object.FindFirstObjectByType<StageManager>();Call(stage,"Start");
                var eq=stage.Equipment;var swords=stage.Swords;var enemies=stage.Enemies;
                Check(stage.Player.Health==10,"Player HP");Check(eq.Inventory.Count==26 && eq.EquippedCount==20,"Starting equipment");
                eq.SetAllEquipped(true);Check(Mathf.Approximately(eq.SpeedMultiplier,1.3f)&&Mathf.Approximately(eq.RecallMultiplier,1.5f),"Stacked bonuses");
                var speed=eq.Inventory.First(x=>x.Kind==SwordKind.Speed);eq.SetEquipped(speed,false);Check(Mathf.Approximately(eq.SpeedMultiplier,1.15f),"Unequip invalidates bonuses");eq.SetEquipped(speed,true);
                var geometry=UnityEngine.Object.FindFirstObjectByType<StageGeometry>();
                Check(geometry.ClipTerrain(new Vector2(0,0),new Vector2(0,-20),Vector2.zero,out var floor),"Terrain sweep");
                Check(floor.y>-7,"Floor contact position");
                var s=eq.Inventory.First();s.Root.localPosition=new Vector2(-12,0);Call(swords,"BeginFlight",s,new Vector2(-10,0));Call(swords,"AdvanceFlight",s,1f);Check(s.State==SwordState.Falling,"Arrived sword falls");
                s.Root.localPosition=new Vector2(-12,-5);s.FallVelocity=Vector2.zero;Call(swords,"AdvanceFall",s,1f);Check(s.State==SwordState.Planted,"Falling sword plants");Call(swords,"AdvancePlanted",s,3.1f);Check(s.State==SwordState.Returning,"Planted sword auto recall");
                var fused=eq.Inventory.First(x=>x.Kind==SwordKind.LockOn);fused.FusionSize=3;fused.FusedKinds.AddRange(new[]{SwordKind.LockOn,SwordKind.Speed,SwordKind.Recall});
                Check((int)Call(swords,"SwordDamage",fused)==3,"Fusion damage");Check((bool)Call(swords,"SwordMarksTarget",fused),"Fusion retains mark");var parts=(List<SwordInstance>)Call(swords,"SeparateSword",fused);Check(parts.Select(x=>x.Kind).SequenceEqual(new[]{SwordKind.LockOn,SwordKind.Speed,SwordKind.Recall}),"Fusion restores types");Check(parts.All(x=>x.BlocksRemaining==1&&x.FusionSize==0),"Split resets block count");
                enemies.Tick(5.1f);Check(enemies.Enemies.Count==3,"Wave has three enemies");Check(enemies.Enemies.All(x=>x.Health==30),"Enemy HP");Check(enemies.Enemies.All(x=>!geometry.OverlapsTerrain(x.Position,Vector2.one*.65f)),"Spawn outside terrain");
                var first=enemies.Enemies[0];var oldView=first.View;enemies.Damage(first,30);Check(first.Health==0&&!oldView.gameObject.activeSelf,"Enemy defeat returns view");
                while(enemies.Enemies.Count>0)enemies.Damage(enemies.Enemies[0],30);enemies.Tick(4.9f);Check(enemies.Enemies.Count==0,"Respawn waits five seconds");enemies.Tick(.2f);Check(enemies.Enemies.Count==3,"Respawn wave");Check(enemies.Enemies.Any(x=>x.View==oldView),"Enemy pool reuses view");
                Call(enemies,"SpawnBullet",new Vector2(-11,0));var b=enemies.Bullets[enemies.Bullets.Count-1];var oldBullet=b.View;var velocity=b.Velocity;stage.Player.transform.position=new Vector2(-12,2);enemies.Tick(.01f);Check(b.Velocity==velocity,"Bullet does not home");enemies.RemoveBullet(enemies.Bullets.Count-1);Call(enemies,"SpawnBullet",new Vector2(-11,0));Check(enemies.Bullets[enemies.Bullets.Count-1].View==oldBullet,"Bullet pool reuses view");
                var ui=UnityEngine.Object.FindFirstObjectByType<UIManager>();Call(ui,"Awake");ui.OpenInventory();var panel=UnityEngine.Object.FindFirstObjectByType<InventoryPanel>();Check(panel!=null&&Time.timeScale==0,"Bag pauses game");Canvas.ForceUpdateCanvases();
                var card=panel.GetComponentsInChildren<SwordSlot>().First(x=>x.Sword.State!=SwordState.Stored);var section=Field<InventorySection>(panel,"storageSection");var pointer=new PointerEventData(UnityEngine.Object.FindFirstObjectByType<EventSystem>()){pointerDrag=card.gameObject,button=PointerEventData.InputButton.Left};
                ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(section.gameObject,pointer,ExecuteEvents.dropHandler);ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.endDragHandler);Check(card.Sword.State==SwordState.Stored,"Card drop moves equipment");ui.Resume();Check(Time.timeScale==1,"Resume restores time");
                stage.Player.ResetPlayer();stage.Player.TakeHit();stage.Player.TakeHit();Check(stage.Player.Health==9,"Hit invulnerability");
            }
            Check(EditorBuildSettings.scenes.Count(x=>x.enabled)==3,"Three build scenes");result="PASS: "+assertions+" regression assertions across both stages.";Debug.Log(result);
        }
        catch(Exception exception){result="FAIL: "+exception;Debug.LogException(exception);throw;}
        finally {Time.timeScale=1;EditorSceneManager.OpenScene("Assets/Scenes/Title.unity");Directory.CreateDirectory("Logs");File.WriteAllText("Logs/RefactorChecks.txt",result);}
    }
}
