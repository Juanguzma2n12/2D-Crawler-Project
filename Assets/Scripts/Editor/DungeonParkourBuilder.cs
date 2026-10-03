using System;
using System.Collections.Generic;
using Cainos.InteractivePixelWater;
using Cainos.PixelArtPlatformer_VillageProps;
using Platformer.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Platformer.EditorTools
{
    // Tools > Dungeon > Build Parkour Area
    //
    // Builds a dungeon parkour course in empty space to the right of the level, plus a teleport stone next to
    // the Bandit that takes you there (stand on it and press Up). Running it again replaces the old one.
    //
    // The dungeon is a solid block of rock with rooms carved out of it. Coordinates below are in tiles,
    // relative to the dungeon's origin; y = 0 is the floor of the entrance hall. Tune the course in Layout().
    public class DungeonParkourBuilder
    {
        private const string RootName = "Dungeon Parkour";
        private const string VillageTeleporterName = "Dungeon Teleporter";

        private const string PropsFolder = "Assets/Cainos/Pixel Art Platformer - Village Props/";
        private const string PropPrefix = PropsFolder + "Prefab/PF Village Props - ";
        private const string TilePrefix = PropsFolder + "Tileset Palette/TP Ground/TX Tileset Ground_";
        private const string WaterPrefabPath = "Assets/Cainos/Interactive Pixel Water/Prefab/PF Pixel Water.prefab";

        // Tile ids in TX Tileset Ground: the 3x3 grass-topped dirt block, and the thin floating ledge
        private const int TopLeft = 0, Top = 1, TopRight = 2;
        private const int Left = 3, Center = 4, Right = 5;
        private const int BottomLeft = 6, Bottom = 7, BottomRight = 8;
        private const int LedgeLeft = 108, LedgeMid = 109, LedgeRight = 110;

        // Extent of the rock block, in tiles
        private const int MinX = 0, MaxX = 140, MinY = -9, MaxY = 14;

        private static readonly Color BackgroundTint = new Color(0.3f, 0.26f, 0.3f);

        private readonly HashSet<Vector2Int> solid = new HashSet<Vector2Int>();
        private readonly HashSet<Vector2Int> ledges = new HashSet<Vector2Int>();
        private readonly Dictionary<int, TileBase> tiles = new Dictionary<int, TileBase>();

        private Vector3 origin;
        private Transform root;
        private Transform mechanisms;
        private Transform hazards;
        private Transform checkpoints;
        private Transform decor;
        private Transform entrance;

        [MenuItem("Tools/Dungeon/Build Parkour Area")]
        private static void BuildMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            Bandit bandit = FindInScene<Bandit>(scene);
            if (bandit == null)
            {
                EditorUtility.DisplayDialog("Build Parkour Area",
                    $"No Bandit found in the active scene ({scene.name}). Make the scene with your Bandit the active scene and try again.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Build Dungeon Parkour");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject oldRoot = FindRootObject(scene, RootName);
            GameObject oldTeleporter = FindRootObject(scene, VillageTeleporterName);
            if (oldRoot != null || oldTeleporter != null)
            {
                if (!EditorUtility.DisplayDialog("Build Parkour Area",
                        "This scene already has a dungeon parkour area. Replace it with a fresh one?", "Replace", "Cancel"))
                    return;

                if (oldRoot != null) Undo.DestroyObjectImmediate(oldRoot);
                if (oldTeleporter != null) Undo.DestroyObjectImmediate(oldTeleporter);
            }

            var builder = new DungeonParkourBuilder();
            try
            {
                builder.Build(bandit);
            }
            catch (Exception e)
            {
                if (builder.root != null) Object.DestroyImmediate(builder.root.gameObject);
                GameObject partialTeleporter = FindRootObject(scene, VillageTeleporterName);
                if (partialTeleporter != null) Object.DestroyImmediate(partialTeleporter);
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Build Parkour Area", "Building the parkour area failed:\n\n" + e.Message, "OK");
                return;
            }

            Undo.CollapseUndoOperations(undoGroup);
            EditorSceneManager.MarkSceneDirty(scene);

            Selection.activeGameObject = builder.root.gameObject;
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();

            Debug.Log($"Dungeon parkour built at {builder.origin}. Stand on the stone next to the Bandit and press Up to enter. Save the scene (Ctrl+S) to keep it.");
        }

        private void Build(Bandit bandit)
        {
            origin = FindOrigin(bandit);

            var rootGO = new GameObject(RootName, typeof(Grid));
            rootGO.transform.position = origin;
            root = rootGO.transform;
            Undo.RegisterCreatedObjectUndo(rootGO, "Build Dungeon Parkour");

            mechanisms = CreateGroup("Mechanisms");
            hazards = CreateGroup("Hazards");
            checkpoints = CreateGroup("Checkpoints");
            decor = CreateGroup("Decor");

            entrance = CreateGroup("Entrance");
            entrance.position = W(5.5f, 0.1f);

            Layout();
            BuildTilemaps();
            AddFallCatcher();
            ConnectToVillage(bandit);
        }

        // -- The course --

        private void Layout()
        {
            Fill(MinX, MinY, MaxX, MaxY);

            // 1. Entrance hall
            Carve(2, 0, 13, 5);
            AddCheckpoint(8f, 0);
            Torch(11f, 0);

            // 2. Pillar hops over a spike pit (the middle pillar is one tile higher)
            Carve(14, -6, 29, 5);
            Fill(17, -6, 18, -1);
            Fill(21, -6, 22, 0);
            Fill(25, -6, 26, -1);
            SpikeRow(14, 16, -6);
            SpikeRow(19, 20, -6);
            SpikeRow(23, 24, -6);
            SpikeRow(27, 29, -6);

            // 3. Climb the floating ledges, spikes below
            Carve(30, -3, 52, 9);
            Fill(30, -3, 31, -1);
            AddCheckpoint(31f, 0);
            Ledge(34, 0, 3);
            Ledge(39, 1, 3);
            Ledge(44, 2, 3);
            Ledge(49, 3, 3);
            SpikeRow(32, 52, -3);

            // 4. Spike corridor
            Carve(53, 4, 72, 7);
            AddCheckpoint(54.5f, 4);
            SpikeRow(57, 58, 4);
            Torch(60f, 4);
            SpikeRow(62, 64, 4);
            Torch(66f, 4);
            SpikeRow(68, 69, 4);
            Torch(71f, 4);

            // 5. Flooded room: drop into the water and dive under the hanging wall
            Carve(73, -5, 92, 7);
            Fill(81, -3, 82, 7);
            Fill(88, -5, 92, -2);
            AddWater(73, -5, 87, -2);
            AddCheckpoint(90.5f, -1);

            // 6. Bounce pad up the shaft
            Carve(93, -1, 98, 10);
            BouncePad(95.5f, -1);

            // 7. Ride the lifts across the spike pit
            Carve(99, -4, 120, 6);
            Fill(99, -4, 104, 2);
            AddCheckpoint(101.5f, 3);
            SpikeRow(105, 120, -4);
            AddElevator(108.5f, 7, 3, -1);
            OneWayPlatform("Platform 02 X3", 112, 3);
            AddElevator(117.5f, 7, 5, 1);

            // 8. Treasure room
            Carve(121, 3, 137, 9);
            AddCheckpoint(123f, 3);
            Torch(126f, 3);
            PlaceOnFloor("Fire Bowl", decor, 128f, 3);
            AddChest(130.5f, 3);
            PlaceOnFloor("Fire Bowl", decor, 133f, 3);
            Torch(136.5f, 3);
        }

        // -- Layout helpers (tiles, inclusive ranges) --

        private void Fill(int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    solid.Add(new Vector2Int(x, y));
        }

        private void Carve(int x0, int y0, int x1, int y1)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    solid.Remove(new Vector2Int(x, y));
        }

        // A thin floating ledge; its top surface is at y + 1
        private void Ledge(int x0, int y, int length)
        {
            for (int x = x0; x < x0 + length; x++)
                ledges.Add(new Vector2Int(x, y));
        }

        // -- Objects --

        private void AddCheckpoint(float x, float floorY)
        {
            var go = new GameObject("Checkpoint");
            go.transform.SetParent(checkpoints, false);
            go.transform.position = W(x, floorY);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.5f, 2.5f);
            col.offset = new Vector2(0f, 1.25f);
            var checkpoint = go.AddComponent<Checkpoint>();

            // A fire bowl that lights up when the checkpoint is reached
            GameObject bowl = PlaceOnFloor("Fire Bowl", go.transform, x, floorY);
            PrefabUtility.UnpackPrefabInstance(bowl, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var lit = new GameObject("Lit");
            lit.transform.SetParent(bowl.transform, false);
            foreach (string fx in new[] { "Fire", "Glow", "Spark" })
            {
                Transform t = bowl.transform.Find(fx);
                if (t != null) t.SetParent(lit.transform, true);
            }

            var so = new SerializedObject(checkpoint);
            so.FindProperty("activeVisualGlow").objectReferenceValue = lit;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private void Torch(float x, float floorY)
        {
            PlaceOnFloor("Torch", decor, x, floorY);
        }

        // Spikes on the floor tiles x0..x1, with one kill zone covering the row
        private void SpikeRow(int x0, int x1, int floorY)
        {
            var row = new GameObject("Spikes");
            row.transform.SetParent(hazards, false);
            row.transform.position = W((x0 + x1 + 1) * 0.5f, floorY);

            for (int x = x0; x <= x1; x++)
                PlaceOnFloor("Spike", row.transform, x + 0.5f, floorY);

            var col = row.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(x1 - x0 + 1 - 0.2f, 0.35f);
            col.offset = new Vector2(0f, 0.175f);
            row.AddComponent<KillZone>();
        }

        private void AddWater(int x0, int y0, int x1, int y1)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WaterPrefabPath);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + WaterPrefabPath);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, mechanisms);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            // The water's pivot is at the bottom center
            int width = x1 - x0 + 1;
            int height = y1 - y0 + 1;
            go.transform.position = W(x0 + width * 0.5f, y0);
            go.GetComponent<PixelWater>().Size = new Vector2(width, height);
        }

        private void BouncePad(float x, float floorY)
        {
            GameObject pad = InstantiateProp("Bounding Platform 01", mechanisms);
            pad.transform.position = W(x, floorY);
            RecordOverrides(pad.transform);
        }

        // A lift hanging from the ceiling that moves its platform between the top and bottom floor heights
        private void AddElevator(float x, int ceilingY, int topStop, int bottomStop)
        {
            GameObject go = InstantiateProp("Elevator", mechanisms);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.transform.position = W(x, ceilingY);

            var elevator = go.GetComponent<Elevator>();
            elevator.lengthRange = new Vector2(ceilingY - topStop, ceilingY - bottomStop);
            elevator.moveSpeed = 2.0f;
            elevator.Length = elevator.lengthRange.y;   // preview the platform where it starts
        }

        // A jump-through wooden platform whose walkable top starts at the left edge of tile x0
        private void OneWayPlatform(string prefabName, int x0, float topY)
        {
            GameObject go = InstantiateProp(prefabName, mechanisms);
            Vector3 target = W(x0, topY);
            go.transform.position = target;

            foreach (BoxCollider2D col in go.GetComponentsInChildren<BoxCollider2D>())
            {
                if (col.isTrigger) continue;
                Vector3 min = col.transform.TransformPoint(col.offset - col.size * 0.5f);
                Vector3 max = col.transform.TransformPoint(col.offset + col.size * 0.5f);
                go.transform.position += new Vector3(target.x - min.x, target.y - max.y, 0f);
                break;
            }
            RecordOverrides(go.transform);
        }

        private void AddChest(float x, float floorY)
        {
            var go = new GameObject("Treasure Chest");
            go.transform.SetParent(decor, false);
            go.transform.position = W(x, floorY);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2f, 1.5f);
            col.offset = new Vector2(0f, 0.75f);
            var interact = go.AddComponent<ChestInteract>();

            GameObject chest = PlaceOnFloor("Chest Golden", go.transform, x, floorY);
            var so = new SerializedObject(interact);
            so.FindProperty("chest").objectReferenceValue = chest.GetComponentInChildren<Chest>();
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Safety net under the whole dungeon
        private void AddFallCatcher()
        {
            var go = new GameObject("Fall Catcher");
            go.transform.SetParent(hazards, false);
            go.transform.position = W((MinX + MaxX) * 0.5f, MinY - 3);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(MaxX - MinX + 20, 2f);
            go.AddComponent<KillZone>();
        }

        // Teleport stones: one next to the Bandit leading in, and two in the dungeon leading back
        private void ConnectToVillage(Bandit bandit)
        {
            Vector3 banditPos = bandit.transform.position;
            float stoneX = banditPos.x + 2.5f;
            float groundY = FindGroundY(stoneX, banditPos.y);

            var villageGO = new GameObject(VillageTeleporterName);
            SceneManager.MoveGameObjectToScene(villageGO, bandit.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(villageGO, "Build Dungeon Parkour");
            Teleporter villageTeleporter = AddTeleporter(villageGO, new Vector3(stoneX, groundY, 0f));

            var arrival = new GameObject("Arrival").transform;
            arrival.SetParent(villageGO.transform, false);
            arrival.position = new Vector3(stoneX - 1.5f, groundY + 0.1f, 0f);

            SetDestination(villageTeleporter, entrance);

            var returnGO = new GameObject("Return Stone");
            returnGO.transform.SetParent(decor, false);
            SetDestination(AddTeleporter(returnGO, W(3.5f, 0)), arrival);

            var treasureReturnGO = new GameObject("Return Stone");
            treasureReturnGO.transform.SetParent(decor, false);
            SetDestination(AddTeleporter(treasureReturnGO, W(134.5f, 3)), arrival);
        }

        private Teleporter AddTeleporter(GameObject go, Vector3 floorPoint)
        {
            go.transform.position = floorPoint;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(1.2f, 2f);
            col.offset = new Vector2(0f, 1f);
            var teleporter = go.AddComponent<Teleporter>();

            PlaceOnFloor("Stone of Recall", go.transform, floorPoint);
            return teleporter;
        }

        private static void SetDestination(Teleporter teleporter, Transform destination)
        {
            var so = new SerializedObject(teleporter);
            so.FindProperty("destination").objectReferenceValue = destination;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // -- Tilemaps --

        private void BuildTilemaps()
        {
            TilemapRenderer template = FindTemplateRenderer();
            int baseOrder = template != null ? template.sortingOrder : 0;

            Tilemap background = CreateTilemap("Background Wall", baseOrder - 5, BackgroundTint, template);
            Tilemap walls = CreateTilemap("Walls", baseOrder, Color.white, template);

            // Merge the tile colliders into one outline so the Bandit doesn't snag on seams between tiles
            var body = walls.gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            walls.gameObject.AddComponent<CompositeCollider2D>();
            var tileCollider = walls.gameObject.AddComponent<TilemapCollider2D>();
            tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;

            var wallCells = new List<Vector3Int>();
            var wallTiles = new List<TileBase>();
            var backCells = new List<Vector3Int>();
            var backTiles = new List<TileBase>();

            for (int x = MinX; x <= MaxX; x++)
            {
                for (int y = MinY; y <= MaxY; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (solid.Contains(cell))
                    {
                        wallCells.Add(new Vector3Int(x, y, 0));
                        wallTiles.Add(GetTile(SolidTileId(cell)));
                        continue;
                    }

                    backCells.Add(new Vector3Int(x, y, 0));
                    backTiles.Add(GetTile(Center));

                    if (ledges.Contains(cell))
                    {
                        bool left = ledges.Contains(cell + Vector2Int.left);
                        bool right = ledges.Contains(cell + Vector2Int.right);
                        int id = left && !right ? LedgeRight : !left && right ? LedgeLeft : LedgeMid;
                        wallCells.Add(new Vector3Int(x, y, 0));
                        wallTiles.Add(GetTile(id));
                    }
                }
            }

            walls.SetTiles(wallCells.ToArray(), wallTiles.ToArray());
            background.SetTiles(backCells.ToArray(), backTiles.ToArray());
        }

        // Picks the edge/corner piece of the 3x3 block based on which neighbours are open
        private int SolidTileId(Vector2Int cell)
        {
            bool up = solid.Contains(cell + Vector2Int.up);
            bool down = solid.Contains(cell + Vector2Int.down);
            bool left = solid.Contains(cell + Vector2Int.left);
            bool right = solid.Contains(cell + Vector2Int.right);

            if (!up) return !left ? TopLeft : !right ? TopRight : Top;
            if (!down) return !left ? BottomLeft : !right ? BottomRight : Bottom;
            if (!left) return Left;
            if (!right) return Right;
            return Center;
        }

        private TileBase GetTile(int id)
        {
            if (tiles.TryGetValue(id, out TileBase tile)) return tile;

            string path = TilePrefix + id + ".asset";
            tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
            if (tile == null) throw new InvalidOperationException("Missing tile: " + path);
            tiles[id] = tile;
            return tile;
        }

        private Tilemap CreateTilemap(string name, int sortingOrder, Color color, TilemapRenderer template)
        {
            var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer));
            go.transform.SetParent(root, false);

            var tilemap = go.GetComponent<Tilemap>();
            tilemap.color = color;

            var renderer = go.GetComponent<TilemapRenderer>();
            if (template != null)
            {
                renderer.sharedMaterial = template.sharedMaterial;
                renderer.sortingLayerID = template.sortingLayerID;
            }
            renderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        // Match the look (material, sorting layer) of the level's existing ground tilemap
        private TilemapRenderer FindTemplateRenderer()
        {
            foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (go.transform == root) continue;
                foreach (TilemapCollider2D col in go.GetComponentsInChildren<TilemapCollider2D>())
                {
                    var renderer = col.GetComponent<TilemapRenderer>();
                    if (renderer != null) return renderer;
                }
            }
            return null;
        }

        // -- Utilities --

        // Dungeon tile coordinates to world position
        private Vector3 W(float x, float y)
        {
            return origin + new Vector3(x, y, 0f);
        }

        private Transform CreateGroup(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(root, false);
            return t;
        }

        private static GameObject InstantiateProp(string prefabName, Transform parent)
        {
            string path = PropPrefix + prefabName + ".prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing prefab: " + path);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        }

        private GameObject PlaceOnFloor(string prefabName, Transform parent, float x, float floorY)
        {
            return PlaceOnFloor(prefabName, parent, W(x, floorY));
        }

        // Places a prop so the bottom center of its sprites stands on the given point
        private static GameObject PlaceOnFloor(string prefabName, Transform parent, Vector3 point)
        {
            GameObject go = InstantiateProp(prefabName, parent);
            go.transform.position = point;

            bool hasBounds = false;
            var bounds = new Bounds();
            foreach (SpriteRenderer sr in go.GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr.sprite == null) continue;
                if (hasBounds) bounds.Encapsulate(sr.bounds);
                else { bounds = sr.bounds; hasBounds = true; }
            }
            if (hasBounds)
                go.transform.position += new Vector3(point.x - bounds.center.x, point.y - bounds.min.y, 0f);

            RecordOverrides(go.transform);
            return go;
        }

        private static void RecordOverrides(Object obj)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }

        // Put the dungeon in empty space to the right of everything in the loaded scenes, level with the Bandit
        private static Vector3 FindOrigin(Bandit bandit)
        {
            float maxX = bandit.transform.position.x;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;

                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
                    {
                        // Skip huge backdrops (sky, parallax layers) so they don't push the dungeon far away
                        if (!(r is TilemapRenderer) && r.bounds.size.x > 60f) continue;
                        maxX = Mathf.Max(maxX, r.bounds.max.x);
                    }
                }
            }

            return new Vector3(Mathf.Ceil(maxX) + 12f, Mathf.Round(bandit.transform.position.y), 0f);
        }

        private static float FindGroundY(float x, float banditY)
        {
            Physics2D.SyncTransforms();
            foreach (RaycastHit2D hit in Physics2D.RaycastAll(new Vector2(x, banditY + 1f), Vector2.down, 6f))
            {
                if (hit.collider.isTrigger) continue;
                if (hit.collider.attachedRigidbody != null && hit.collider.attachedRigidbody.GetComponent<Bandit>() != null) continue;
                return hit.point.y;
            }
            return Mathf.Round(banditY);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                T found = go.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }

        private static GameObject FindRootObject(Scene scene, string name)
        {
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go;
            }
            return null;
        }
    }
}
