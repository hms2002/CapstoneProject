// Run in an isolated Unity Editor project with the current compiled project assemblies.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityGAS;
using Object = UnityEngine.Object;
public static class SlimeNavigationRecoveryRegression
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Flags).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Flags).Invoke(target, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    public static void Run()
    {
        try { Landing(); ReachableGoal(); StationaryOverlap(); Progress(); Debug.Log("SLIME_NAVIGATION_RECOVERY_PASS: unsnapped/same-tile/ground, reachable alternative, stationary overlap and simulation exclusion, stall/recovery."); EditorApplication.Exit(0); }
        catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static TilemapPathfinder2D Finder(GameObject root, params Vector3Int[] cells)
    {
        var grid = root.AddComponent<Grid>();
        var ground = new GameObject("Ground"); ground.transform.SetParent(root.transform);
        var map = ground.AddComponent<Tilemap>(); var tile = ScriptableObject.CreateInstance<Tile>();
        foreach(var cell in cells) map.SetTile(cell, tile);
        var finder = root.AddComponent<TilemapPathfinder2D>(); Set(finder,"grid",grid); Set(finder,"groundTilemap",map);
        return finder;
    }
    static void Landing()
    {
        var grid = new GameObject("Landing grid"); var actor = new GameObject("Child");
        try {
            var finder=Finder(grid,Vector3Int.zero); var body=actor.AddComponent<Rigidbody2D>(); body.gravityScale=0;
            actor.AddComponent<BoxCollider2D>().size=Vector2.one*0.2f;
            var planner=new SlimeSplitPlacement2D(actor,null,1<<30,0.08f,8,null);
            var origin=new Vector2(0.4f,0.4f);
            Check(planner.TryResolveTile(origin,Vector2.right,0.1f,finder,out _,out var right),"right rejected");
            Check(Vector2.Distance(right,new Vector2(0.5f,0.4f))<0.001f,"right snapped");
            Check(planner.TryResolveTile(origin,Vector2.left,0.1f,finder,out _,out var left),"same tile rejected");
            Check(Vector2.Distance(left,new Vector2(0.3f,0.4f))<0.001f,"left snapped");
            Check(planner.TryResolveTile(origin,Vector2.right,0,finder,out _,out var still)&&still==origin,"own position rejected");
            Check(!finder.IsValidLandingPosition(new Vector2(3,3),new MonsterNavigationFootprint2D(Vector2.one*0.2f,Vector2.zero)),"missing ground accepted");
        } finally { Object.DestroyImmediate(actor);Object.DestroyImmediate(grid); }
    }
    static void ReachableGoal()
    {
        var root=new GameObject("Disconnected goal grid");
        try {
            var finder=Finder(root,new Vector3Int(1,-1),new Vector3Int(3,0),new Vector3Int(4,0),new Vector3Int(5,0));
            Check(finder.TryBuildPath(new Vector2(5.5f,0.5f),new Vector2(2.5f,0.5f),out var path,new MonsterNavigationFootprint2D(Vector2.one*0.2f,Vector2.zero)),"reachable alternative ignored");
            Check(path.Count>1 && path[path.Count-1].x>=3.5f,"selected disconnected candidate");
            Check(finder.TryBuildPath(new Vector2(4.5f,0.5f),new Vector2(2.5f,0.5f),out path,new MonsterNavigationFootprint2D(Vector2.one*0.2f,Vector2.zero)),"nearby route failed");
            Check(path.Count>1 && path[path.Count-1]==new Vector2(3.5f,0.5f),"stopped at own cell despite a nearer reachable goal");
        } finally {Object.DestroyImmediate(root);}
    }
    static void StationaryOverlap()
    {
        var actor=new GameObject("Motor"); actor.SetActive(false);
        var wall=new GameObject("Wall"); wall.layer=30;wall.transform.position=new Vector3(1.5f,0);var obstacle=wall.AddComponent<BoxCollider2D>();obstacle.size=new Vector2(1,10);
        try {
            var body=actor.AddComponent<Rigidbody2D>();body.gravityScale=0;var shape=actor.AddComponent<BoxCollider2D>();shape.size=Vector2.one*0.5f;
            var motor=actor.AddComponent<MovementMotor2D>();motor.enabled=false;Set(motor,"body",body);Set(motor,"wallCollisionLayers",(LayerMask)(1<<30));actor.SetActive(true);
            Call(motor,"CacheBodyColliders");Call(motor,"ConfigureWallContactFilter");
            body.position=new Vector2(0.8f,0);actor.transform.position=body.position;Physics2D.SyncTransforms();
            Check(shape.Distance(obstacle).isOverlapped,"invalid overlap baseline");
            Call(motor,"ResolveWallSafeVelocity",Vector2.zero);actor.transform.position=body.position;Physics2D.SyncTransforms();
            Check(!shape.Distance(obstacle).isOverlapped,"stationary overlap persists");
            body.simulated=false;body.position=new Vector2(0.8f,0);var before=body.position;
            Call(motor,"ResolveWallSafeVelocity",Vector2.zero);Check(body.position==before,"airborne body moved");
        } finally {Object.DestroyImmediate(actor);Object.DestroyImmediate(wall);}
    }
    static void Progress()
    {
        var actor=new GameObject("Chase progress");actor.SetActive(false);
        try {
            var chase=actor.AddComponent<EnemyChaseIntent2D>();Set(chase,"lastProgressSampleTime",Time.time);Set(chase,"progressTime",Time.time-2);Set(chase,"progressPosition",Vector2.zero);
            Call(chase,"ObserveChaseProgress");Check((bool)typeof(EnemyChaseIntent2D).GetField("chaseStalled",Flags).GetValue(chase),"stall missed");
            actor.transform.position=Vector2.right;Call(chase,"ObserveChaseProgress");Check(!(bool)typeof(EnemyChaseIntent2D).GetField("chaseStalled",Flags).GetValue(chase),"progress did not clear stall");
        } finally {Object.DestroyImmediate(actor);}
    }
}
