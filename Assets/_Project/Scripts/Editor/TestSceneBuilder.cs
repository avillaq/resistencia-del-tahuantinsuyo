using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using ResistenciaTahuantinsuyo.Runtime.AI;
using ResistenciaTahuantinsuyo.Runtime.Player;

namespace ResistenciaTahuantinsuyo.Editor
{
    public static class TestSceneBuilder
    {
        [MenuItem("Tahuantinsuyo/Build Test Scene (SCN_Test_AI)")]
        public static void BuildScene()
        {
            string scenePath = "Assets/_Project/Scenes/Test/SCN_Test_AI.unity";
            var scene = EditorSceneManager.OpenScene(scenePath);

            // Limpiar objetos anteriores
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
            {
                Object.DestroyImmediate(r);
            }

            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer == -1) obstacleLayer = 8;

            // 1. Cargar Assets de Sprites y Tiles
            Sprite sprPlayer = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Player/hero1.png");
            Sprite sprGuard = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Characters/Enemies/guard_1.png");
            Sprite sprAlert = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/SPR_Alert.png");
            Sprite sprMarker = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/SPR_Marker.png");

            // Tiles de Dungeon
            Tile tileFloor = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_1_2.asset");
            Tile tileFloorAlt = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_2_2.asset");
            Tile tileWallTop = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_1_4.asset");
            Tile tileWallFace = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_1_3.asset");
            Tile tileWallBottom = AssetDatabase.LoadAssetAtPath<Tile>("Assets/_Project/Tilemaps/Tiles/Dungeon/TILE_dungeon_1_0.asset");

            // Configurar tipos de colisión de tiles
            if (tileFloor != null) { tileFloor.colliderType = Tile.ColliderType.None; EditorUtility.SetDirty(tileFloor); }
            if (tileFloorAlt != null) { tileFloorAlt.colliderType = Tile.ColliderType.None; EditorUtility.SetDirty(tileFloorAlt); }
            if (tileWallTop != null) { tileWallTop.colliderType = Tile.ColliderType.Grid; EditorUtility.SetDirty(tileWallTop); }
            if (tileWallFace != null) { tileWallFace.colliderType = Tile.ColliderType.Grid; EditorUtility.SetDirty(tileWallFace); }
            if (tileWallBottom != null) { tileWallBottom.colliderType = Tile.ColliderType.Grid; EditorUtility.SetDirty(tileWallBottom); }
            AssetDatabase.SaveAssets();

            // 2. Cámara Principal
            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.position = new Vector3(0, 0, -10);
            Camera cam = camObj.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.0f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.14f, 0.12f); // Fondo andino
            camObj.AddComponent<AudioListener>();

            // 3. Luz 2D Global (URP 2D)
            GameObject lightObj = new GameObject("Global 2D Light");
            var light2D = lightObj.AddComponent<Light2D>();
            light2D.lightType = Light2D.LightType.Global;
            light2D.intensity = 1.15f;
            light2D.color = new Color(1.0f, 0.97f, 0.92f);

            // 4. Grid y Tilemaps
            GameObject gridObj = new GameObject("Grid");
            var grid = gridObj.AddComponent<Grid>();
            grid.cellSize = new Vector3(1f, 1f, 0f);

            // 4A. Tilemap_Ground (Suelo)
            GameObject groundObj = new GameObject("Tilemap_Ground");
            groundObj.transform.parent = gridObj.transform;
            groundObj.transform.localPosition = Vector3.zero;
            var groundTilemap = groundObj.AddComponent<Tilemap>();
            var groundRenderer = groundObj.AddComponent<TilemapRenderer>();
            groundRenderer.sortingOrder = -10;

            int minX = -13, maxX = 12;
            int minY = -9, maxY = 8;
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    Tile chosenFloor = ((x + y) % 5 == 0 && tileFloorAlt != null) ? tileFloorAlt : tileFloor;
                    groundTilemap.SetTile(new Vector3Int(x, y, 0), chosenFloor);
                }
            }

            // 4B. Tilemap_Obstacles (Muros y Coberturas)
            GameObject obstaclesObj = new GameObject("Tilemap_Obstacles");
            obstaclesObj.transform.parent = gridObj.transform;
            obstaclesObj.transform.localPosition = Vector3.zero;
            obstaclesObj.layer = obstacleLayer;

            var obsTilemap = obstaclesObj.AddComponent<Tilemap>();
            var obsRenderer = obstaclesObj.AddComponent<TilemapRenderer>();
            obsRenderer.sortingOrder = 0;

            var tilemapCollider = obstaclesObj.AddComponent<TilemapCollider2D>();
            tilemapCollider.usedByComposite = true;

            var compositeCollider = obstaclesObj.AddComponent<CompositeCollider2D>();
            compositeCollider.geometryType = CompositeCollider2D.GeometryType.Polygons;

            var obsRb = obstaclesObj.GetComponent<Rigidbody2D>();
            obsRb.bodyType = RigidbodyType2D.Static;

            void PaintWallRect(int startX, int startY, int width, int height)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    for (int y = startY; y < startY + height; y++)
                    {
                        Tile t = (y == startY + height - 1 && tileWallTop != null) ? tileWallTop :
                                 (y == startY && tileWallBottom != null) ? tileWallBottom : tileWallFace;
                        obsTilemap.SetTile(new Vector3Int(x, y, 0), t ?? tileFloor);
                    }
                }
            }

            // Muros perimetrales
            PaintWallRect(minX, maxY, maxX - minX + 1, 1);     // Muro Norte
            PaintWallRect(minX, minY, maxX - minX + 1, 1);     // Muro Sur
            PaintWallRect(minX, minY + 1, 1, maxY - minY - 1); // Muro Oeste
            PaintWallRect(maxX, minY + 1, 1, maxY - minY - 1); // Muro Este

            // Muros y Pilares Interiores (para sigilo y cobertura)
            PaintWallRect(-5, -2, 2, 5); // Pilar Oeste (ancho 2, alto 5)
            PaintWallRect(4, -2, 2, 5);  // Pilar Este (ancho 2, alto 5)
            PaintWallRect(-2, 2, 4, 2);  // Bloque Central Superior (ancho 4, alto 2)

            // Procesar cambios en el tilemap y generar geometría compuesta
            tilemapCollider.ProcessTilemapChanges();
            compositeCollider.GenerateGeometry();

            // 5. Rutas de Patrullaje
            GameObject routesParent = new GameObject("PatrolRoutes");

            // Ruta Oeste
            GameObject routeWestObj = new GameObject("PatrolRoute_West");
            routeWestObj.transform.parent = routesParent.transform;
            var routeWest = routeWestObj.AddComponent<PatrolRoute>();
            Vector3[] wpsWest = new Vector3[] {
                new Vector3(-8f, -5f, 0),
                new Vector3(-8f, 5f, 0),
                new Vector3(-2f, 5f, 0),
                new Vector3(-2f, -5f, 0)
            };
            for (int i = 0; i < wpsWest.Length; i++)
            {
                GameObject wp = new GameObject("WP_" + (i + 1));
                wp.transform.parent = routeWestObj.transform;
                wp.transform.position = wpsWest[i];
            }

            // Ruta Este
            GameObject routeEastObj = new GameObject("PatrolRoute_East");
            routeEastObj.transform.parent = routesParent.transform;
            var routeEast = routeEastObj.AddComponent<PatrolRoute>();
            Vector3[] wpsEast = new Vector3[] {
                new Vector3(2f, -5f, 0),
                new Vector3(8f, -5f, 0),
                new Vector3(8f, 5f, 0),
                new Vector3(2f, 5f, 0)
            };
            for (int i = 0; i < wpsEast.Length; i++)
            {
                GameObject wp = new GameObject("WP_" + (i + 1));
                wp.transform.parent = routeEastObj.transform;
                wp.transform.position = wpsEast[i];
            }

            // 6. Jugador (CHR_Player con sprite real)
            GameObject playerObj = new GameObject("CHR_Player");
            playerObj.tag = "Player";
            playerObj.transform.position = new Vector3(0, -5f, 0);
            playerObj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var playerSr = playerObj.AddComponent<SpriteRenderer>();
            playerSr.sprite = sprPlayer;
            playerSr.color = Color.white;
            playerSr.sortingOrder = 5;

            var playerCol = playerObj.AddComponent<CircleCollider2D>();
            playerCol.radius = 0.35f;

            var playerRb = playerObj.AddComponent<Rigidbody2D>();
            playerRb.gravityScale = 0;
            playerRb.freezeRotation = true;
            playerRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            playerObj.AddComponent<PlayerController>();

            GameObject playerMarker = new GameObject("OrientationMarker");
            playerMarker.transform.parent = playerObj.transform;
            playerMarker.transform.localPosition = new Vector3(0, 0.45f, 0);
            playerMarker.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var pMarkerSr = playerMarker.AddComponent<SpriteRenderer>();
            pMarkerSr.sprite = sprMarker;
            pMarkerSr.color = new Color(0.91f, 0.86f, 0.78f, 0.6f);
            pMarkerSr.sortingOrder = 6;

            PrefabUtility.SaveAsPrefabAsset(playerObj, "Assets/_Project/Prefabs/Player/CHR_Player.prefab");

            // 7. Enemigos (CHR_SpanishGuard con sprite real de guardia)
            var guard1 = BuildGuard("CHR_SpanishGuard_01", new Vector3(-8f, -5f, 0), routeWest, playerObj.transform, sprGuard, sprMarker, sprAlert, obstacleLayer);
            var guard2 = BuildGuard("CHR_SpanishGuard_02", new Vector3(8f, 5f, 0), routeEast, playerObj.transform, sprGuard, sprMarker, sprAlert, obstacleLayer);

            PrefabUtility.SaveAsPrefabAsset(guard1, "Assets/_Project/Prefabs/Enemies/CHR_SpanishGuard.prefab");

            // 8. Guardar Escena
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            Debug.Log("<color=green><b>[Resistencia del Tahuantinsuyo]</b> Escena SCN_Test_AI reconstruida con Tilemaps y sprites reales con éxito.</color>");
        }

        private static GameObject BuildGuard(string name, Vector3 pos, PatrolRoute route, Transform playerTransform, Sprite sprBody, Sprite sprMarker, Sprite sprAlert, int obstacleLayer)
        {
            GameObject guard = new GameObject(name);
            guard.transform.position = pos;
            guard.transform.localScale = new Vector3(1.2f, 1.2f, 1f);

            var guardSr = guard.AddComponent<SpriteRenderer>();
            guardSr.sprite = sprBody;
            guardSr.color = Color.white;
            guardSr.sortingOrder = 5;

            var guardCol = guard.AddComponent<CircleCollider2D>();
            guardCol.radius = 0.35f;

            var guardRb = guard.AddComponent<Rigidbody2D>();
            guardRb.gravityScale = 0;
            guardRb.freezeRotation = true;
            guardRb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var perception = guard.AddComponent<EnemyPerception>();
            var soPerc = new SerializedObject(perception);
            soPerc.FindProperty("viewDistance").floatValue = 6.5f;
            soPerc.FindProperty("viewAngle").floatValue = 90f;
            soPerc.FindProperty("obstacleMask").intValue = 1 << obstacleLayer;
            soPerc.FindProperty("targetMask").intValue = 1 << LayerMask.NameToLayer("Default");
            soPerc.FindProperty("targetTag").stringValue = "Player";
            soPerc.ApplyModifiedProperties();

            // Marcador de orientación
            GameObject gMarker = new GameObject("OrientationMarker");
            gMarker.transform.parent = guard.transform;
            gMarker.transform.localPosition = new Vector3(0, 0.45f, 0);
            gMarker.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var gMarkerSr = gMarker.AddComponent<SpriteRenderer>();
            gMarkerSr.sprite = sprMarker;
            gMarkerSr.color = new Color(0.66f, 0.25f, 0.21f, 0.6f);
            gMarkerSr.sortingOrder = 6;

            // Indicador de Alerta
            GameObject alertObj = new GameObject("AlertIndicator");
            alertObj.transform.parent = guard.transform;
            alertObj.transform.localPosition = new Vector3(0, 0.75f, 0);
            alertObj.transform.localScale = new Vector3(1.0f, 1.0f, 1f);
            var alertSr = alertObj.AddComponent<SpriteRenderer>();
            alertSr.sprite = sprAlert;
            alertSr.color = new Color(0.66f, 0.25f, 0.21f); // #A94136 Rojo alerta
            alertSr.sortingOrder = 7;
            alertSr.enabled = false;

            var controller = guard.AddComponent<EnemyController>();
            var soCtrl = new SerializedObject(controller);
            soCtrl.FindProperty("patrolRoute").objectReferenceValue = route;
            soCtrl.FindProperty("targetPlayer").objectReferenceValue = playerTransform;
            soCtrl.FindProperty("bodyRenderer").objectReferenceValue = guardSr;
            soCtrl.FindProperty("alertIndicatorRenderer").objectReferenceValue = alertSr;
            soCtrl.FindProperty("wanderSpeed").floatValue = 2.0f;
            soCtrl.FindProperty("seekSpeed").floatValue = 3.5f;
            soCtrl.FindProperty("waypointWaitTime").floatValue = 1.0f;
            soCtrl.FindProperty("lostSightCooldown").floatValue = 1.5f;
            soCtrl.FindProperty("wanderColor").colorValue = Color.white;
            soCtrl.FindProperty("seekColor").colorValue = new Color(1.0f, 0.6f, 0.6f);
            soCtrl.ApplyModifiedProperties();

            return guard;
        }
    }
}
