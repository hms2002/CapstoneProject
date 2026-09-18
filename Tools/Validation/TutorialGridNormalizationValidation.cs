using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
public static class TutorialGridNormalizationValidation
{
 static List<Vector2> Points(string path, out Bounds bounds) {
  EditorSceneManager.OpenScene(path);
  var wall=GameObject.Find("Wall"); var tiles=wall.GetComponent<Tilemap>();
  wall.GetComponent<TilemapCollider2D>().ProcessTilemapChanges();
  var collider=wall.GetComponent<CompositeCollider2D>(); collider.GenerateGeometry(); Physics2D.SyncTransforms();
  if(collider.pointCount==0)throw new Exception("Missing wall geometry");
  bounds=collider.bounds; var points=new List<Vector2>();
  // Composite path coordinates already include physics scale; apply pose only.
  for(int p=0;p<collider.pathCount;p++) {
   var vertices=new Vector2[collider.GetPathPointCount(p)];collider.GetPath(p,vertices);
   foreach(var v in vertices){Vector3 w=wall.transform.position + wall.transform.rotation * (Vector3)v;points.Add(w);}
  }
  Debug.Log(path+" vertices="+points.Count+" bounds="+bounds);
  return points;
 }
 public static void Run() {
  try {
   var before=Points("Assets/GridBefore.unity",out var oldBounds);
   var after=Points("Assets/GridAfter.unity",out var newBounds);
   float maxDelta=0f;
   foreach(var a in before){float nearest=float.MaxValue;foreach(var v in after)nearest=Mathf.Min(nearest,Vector2.Distance(a,v));maxDelta=Mathf.Max(maxDelta,nearest);}
   Debug.Log("Wall vertex maximum displacement="+maxDelta);
   if(before.Count!=after.Count || maxDelta>.0001f)throw new Exception("Wall world vertices changed");
   if(Vector3.Distance(oldBounds.min,newBounds.min)>.0001f||Vector3.Distance(oldBounds.max,newBounds.max)>.0001f)throw new Exception("Wall bounds changed");
   var grid=GameObject.Find("TutorialTileGrid");if(grid.transform.localScale!=Vector3.one)throw new Exception("Grid not normalized");
   Debug.Log("GRID_NORMALIZATION_PASS: normalized grid and identical wall world collision vertices/bounds (0.0001 unit comparison).");EditorApplication.Exit(0);
  }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
