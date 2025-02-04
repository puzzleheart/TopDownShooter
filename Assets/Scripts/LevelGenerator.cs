﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Pathfinding;

public class LevelGenerator : MonoBehaviour
{
    enum gridSpace { empty, floor, wall };

    private gridSpace[,] grid;
    //GameObject[,] gridObjs;]

    //temporary; used for testing purposes
    //private List<GameObject> gridObjs = new List<GameObject>();

    private Vector3 randomValidFloorTile;
    private List<Vector3> floorTilesPositions = new List<Vector3>();

    private int roomHeight, roomWidth;
    [SerializeField] private Vector2 roomSizeWorldUnits = new Vector2(30, 30);
    private float worldUnitsInOneGridCell = 1;

    struct walker
    {
        public Vector2 dir;
        public Vector2 pos;
    }

    //Making the walkers spawns more often and turn more often should generally make your rooms wider.
    List<walker> walkers;
    [SerializeField] private float chanceWalkerChangeDir = 0.5f;
    [SerializeField] private float chanceWalkerSpawn = 0.05f;
    [SerializeField] private float chanceWalkerDestroy = 0.05f;
    [SerializeField] private int maxWalkers = 10;
    [SerializeField] private float percentToFill = 0.2f;

    [SerializeField] private TileBase floorTile = default;
    [SerializeField] private TileBase wallTile = default;
    //[SerializeField] private TileBase emptySpaceTile = default;

    [SerializeField] private Tilemap groundTilemap = default;
    [SerializeField] private Tilemap obstacleTilemap = default;
    //[SerializeField] private Tilemap spaceTilemap = default;


    [SerializeField] private bool shouldRemoveSingleWalls = true;

    [Header("Enemies related variables")]

    //for percentage, add more of the same enemy ie 4 gunman prefabs and 1 summoner prefab for 80% chance to summon the gunman
    [SerializeField] private Enemy[] enemies = default;

    [SerializeField] private int maxNumberEnemies = 15;
    [SerializeField] private int minNumberEnemies = 6;

    [SerializeField] private float minRadialDistanceBetweenEnemies = 2f;

    [SerializeField] private float minEnemyDistanceFromPlayer = 10f;


    [Header("Chest related variables")]

    [SerializeField] private Weapon[] possibleWeaponDrops = default;
    [SerializeField] private WeaponPickup weaponPickup = default;
    [SerializeField] private int numberOfWeaponDrops = 1;

    [SerializeField] float minChestDistanceFromPlayer = 25f;

    //chance to spawn enemy per tile
    //[SerializeField] float chanceToSpawnEnemy = .05f;

    private Transform playerTransform = default;
    private int enemiesSpawned = 0;


    private void Start()
    {
        playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
        Setup();
        CreateFloors();
        CreateWalls();
        if (shouldRemoveSingleWalls)
        {
            RemoveSingleWalls();
        }
        SpawnLevel();

        //for some reason it's not working if called immediately; the tilemap probably takes a while to recalculate everything
        Invoke("ScanLevel", .5f);
        Invoke("SpawnPlayer", .55f);
        //if you don't wait a while to spawn enemies they all seem to spawn next to each other
        Invoke("SpawnEnemies", 1.5f);
        Invoke("SpawnWeapons", 2f);
    }

    //private void Update()
    //{
    //    //Debug.Log("ENEMIES ALIVE: " + enemiesAlive);

    //    if (Input.GetKeyDown(KeyCode.Alpha9))
    //    {
    //        ClearGrid();
    //        Setup();
    //        CreateFloors();
    //        CreateWalls();
    //        if (shouldRemoveSingleWalls)
    //        {
    //            RemoveSingleWalls();
    //        }
    //        SpawnLevel();
    //    }
    //}

    //void ClearGrid()
    //{
    //    foreach (GameObject go in gridObjs)
    //    {
    //        Destroy(go);
    //    }
    //    gridObjs.Clear();
    //}

    void Setup()
    {
        //Clear the map (ensures we dont overlap)
        groundTilemap.ClearAllTiles();
        obstacleTilemap.ClearAllTiles();

        //find grid size
        roomWidth = Mathf.RoundToInt(roomSizeWorldUnits.x / worldUnitsInOneGridCell);
        roomHeight = Mathf.RoundToInt(roomSizeWorldUnits.y / worldUnitsInOneGridCell);
        //create grid
        grid = new gridSpace[roomWidth, roomHeight];
        //set grid's default state 
        for (int x = 0; x < roomWidth - 1; x++)
        {
            for (int y = 0; y < roomHeight - 1; y++)
            {
                //make every cell "empty"
                grid[x, y] = gridSpace.empty;
            }
        }

        //set first walker
        //init list
        walkers = new List<walker>();
        //create a walker
        walker newWalker = new walker();
        newWalker.dir = RandomDirection();
        //find center of grid
        Vector2 spawnPos = new Vector2(Mathf.RoundToInt(roomWidth / 2.0f),
                                       Mathf.RoundToInt(roomHeight / 2.0f));
        newWalker.pos = spawnPos;
        //add walker to list
        walkers.Add(newWalker);
    }

    void CreateFloors()
    {
        int iterations = 0; //loop will not run forever
        do
        {
            //create floor at every walker's position
            foreach (walker myWalker in walkers)
            {
                grid[(int)myWalker.pos.x, (int)myWalker.pos.y] = gridSpace.floor;
            }

            //chance to destroy walker
            int numberChecks = walkers.Count; //might modify count while in this loop
            for (int i = 0; i < numberChecks; i++)
            {
                //only if it's not the only one, and at a low chance
                if (Random.value < chanceWalkerDestroy && walkers.Count > 1)
                {
                    walkers.RemoveAt(i);
                    break; //only destroy one per iteration
                }
            }

            //chance: walker pick new direction
            for (int i = 0; i < walkers.Count; i++)
            {
                if (Random.value < chanceWalkerChangeDir)
                {
                    walker thisWalker = walkers[i];
                    thisWalker.dir = RandomDirection();
                    walkers[i] = thisWalker;
                }
            }

            //chance: spawn new walker
            numberChecks = walkers.Count; //might modify while in this loop
            for (int i = 0; i < numberChecks; i++)
            {
                //only if # of walkers < max, and at a low chance
                if (Random.value < chanceWalkerSpawn && walkers.Count < maxWalkers)
                {
                    //create a walker at current walker's position
                    walker newWalker = new walker();
                    newWalker.dir = RandomDirection();
                    newWalker.pos = walkers[i].pos;
                    walkers.Add(newWalker);
                }
            }

            //move walkers
            for (int i = 0; i < walkers.Count; i++)
            {
                walker thisWalker = walkers[i];
                thisWalker.pos += thisWalker.dir;
                walkers[i] = thisWalker;
            }

            //avoid boarder of grid
            for (int i = 0; i < walkers.Count; i++)
            {
                walker thisWalker = walkers[i];
                //clamp x,y to leave a 1 space border: leave room for walls
                thisWalker.pos.x = Mathf.Clamp(thisWalker.pos.x, 1, roomWidth - 2);
                thisWalker.pos.y = Mathf.Clamp(thisWalker.pos.y, 1, roomHeight - 2);
                walkers[i] = thisWalker;
            }

            //check to exit loop
            if ((float)NumberOfFloors() / (float)grid.Length > percentToFill)
            {
                break;
            }
            iterations++;
        } while (iterations < 150000);
    }

    void CreateWalls()
    {
        //loop through every grid space
        for (int x = 0; x < roomWidth - 1; x++)
        {
            for (int y = 0; y < roomHeight - 1; y++)
            {
                //if there's a floor, check the spaces around it 
                if (grid[x, y] == gridSpace.floor)
                {
                    //if any surrounding spaces are empty, place a wall
                    if (grid[x, y + 1] == gridSpace.empty)
                    {
                        grid[x, y + 1] = gridSpace.wall;
                    }
                    if (grid[x, y - 1] == gridSpace.empty)
                    {
                        grid[x, y - 1] = gridSpace.wall;
                    }
                    if (grid[x + 1, y] == gridSpace.empty)
                    {
                        grid[x + 1, y] = gridSpace.wall;
                    }
                    if (grid[x - 1, y] == gridSpace.empty)
                    {
                        grid[x - 1, y] = gridSpace.wall;
                    }
                }
            }
        }
    }

    //could be a create obstacle where there is a single wall instead
    void RemoveSingleWalls()
    {
        //loop though every grid space
        for (int x = 0; x < roomWidth - 1; x++)
        {
            for (int y = 0; y < roomHeight - 1; y++)
            {
                //if theres a wall, check the spaces around it
                if (grid[x, y] == gridSpace.wall)
                {
                    //assume all space around wall are floors
                    bool allFloors = true;
                    //check each side to see if they are all floors
                    for (int checkX = -1; checkX <= 1; checkX++)
                    {
                        for (int checkY = -1; checkY <= 1; checkY++)
                        {
                            if (x + checkX < 0 || x + checkX > roomWidth - 1 ||
                                y + checkY < 0 || y + checkY > roomHeight - 1)
                            {
                                //skip checks that are out of range
                                continue;
                            }
                            if ((checkX != 0 && checkY != 0) || (checkX == 0 && checkY == 0))
                            {
                                //skip corners and center
                                continue;
                            }
                            if (grid[x + checkX, y + checkY] != gridSpace.floor)
                            {
                                allFloors = false;
                            }
                        }
                    }
                    if (allFloors)
                    {
                        grid[x, y] = gridSpace.floor;
                    }
                }
            }
        }
    }

    void SpawnLevel()
    {
        for (int x = 0; x < roomWidth; x++)
        {
            for (int y = 0; y < roomHeight; y++)
            {
                switch (grid[x, y])
                {
                    case gridSpace.empty:
                        //Spawn(x, y, emptySpaceTile, spaceTilemap);

                        //for now, I want empty space to be walls
                        Spawn(x, y, wallTile, obstacleTilemap);
                        //grid[x, y] = gridSpace.wall;
                        break;
                    case gridSpace.floor:
                        Spawn(x, y, floorTile, groundTilemap);
                        break;
                    case gridSpace.wall:
                        Spawn(x, y, wallTile, obstacleTilemap);
                        break;
                }
            }
        }
    }


    Vector2 RandomDirection()
    {
        //pick random int between 0 and 3
        int choice = Mathf.FloorToInt(Random.value * 3.99f);
        //use that int to chose a direction
        switch (choice)
        {
            case 0:
                return Vector2.down;
            case 1:
                return Vector2.left;
            case 2:
                return Vector2.up;
            default:
                return Vector2.right;
        }
    }

    int NumberOfFloors()
    {
        int count = 0;
        foreach (gridSpace space in grid)
        {
            if (space == gridSpace.floor)
            {
                count++;
            }
        }
        return count;
    }

    void Spawn(float x, float y, TileBase tile, Tilemap tilemap)
    {
        Vector2 offset = roomSizeWorldUnits / 2.0f;
        //if x and y range from 0 to 30; world units from -15 to 15, for example
        Vector2 spawnPos = new Vector2(x, y) * worldUnitsInOneGridCell - offset;

        //tilemap.SetTile(new Vector3Int((int)spawnPos.x, (int)spawnPos.y, 0), tile);
        tilemap.SetTile(tilemap.WorldToCell(spawnPos), tile);
    }

    void SpawnWeapons()
    {
        int iterations = 1000;

        int weaponsSpawned = 0;

        while (weaponsSpawned < numberOfWeaponDrops)
        {
            randomValidFloorTile = floorTilesPositions[Random.Range(0, floorTilesPositions.Count)];

            float distanceToPlayer = Vector2.Distance(randomValidFloorTile, playerTransform.position);

            //test if enemy is not too close to the player
            if (distanceToPlayer > minChestDistanceFromPlayer)
            {
                Vector3 randomFloor = new Vector3(randomValidFloorTile.x, randomValidFloorTile.y, 0);

                Collider2D[] colliders = Physics2D.OverlapCircleAll(randomFloor, .2f);

                bool canSpawn = true;

                foreach (Collider2D col in colliders)
                {
                    if (col.tag == "Obstacle")
                    {
                        canSpawn = false;
                    }
                }

                if (canSpawn)
                {
                    //Chest spawnedChest = Instantiate(weaponChest, randomFloor, Quaternion.identity) as Chest;
                    WeaponPickup spawnedWeaponPickup = Instantiate(weaponPickup, randomFloor, Quaternion.identity) as WeaponPickup;

                    //choose a random weapon
                    Weapon weaponToSpawn = possibleWeaponDrops[Random.Range(0, possibleWeaponDrops.Length)];
                    //choose a weapon that the player doesn't have
                    int cont = 100;

                    while (WeaponManager.Instance.CheckIfWeaponIsOwned(weaponToSpawn))
                    {
                        cont--;
                        weaponToSpawn = possibleWeaponDrops[Random.Range(0, possibleWeaponDrops.Length)];

                        if (cont <= 0)
                        {
                            break;
                        }
                    }
                    spawnedWeaponPickup.SetWeapon(weaponToSpawn);

                    weaponsSpawned++;
                }
            }

            iterations--;
            if (iterations <= 0)
            {
                Debug.LogError("BROKE SPAWN CHEST ITERATIONS LIMIT! ");
                break;
            }
        }
    }

    void SpawnEnemies()
    {
        int iterations = 10000;

        int enemiesToSpawn = Random.Range(minNumberEnemies, maxNumberEnemies);

        //summon enemies loop
        while (enemiesSpawned < enemiesToSpawn)
        {
            randomValidFloorTile = floorTilesPositions[Random.Range(0, floorTilesPositions.Count)];

            //test distance between enemies from this position
            Collider2D[] colliders = Physics2D.OverlapCircleAll(randomValidFloorTile, minRadialDistanceBetweenEnemies);

            bool hasFoundNearbyEnemy = false;
            foreach (Collider2D collider in colliders)
            {
                if (collider.tag == "Enemy")
                {
                    hasFoundNearbyEnemy = true;
                }
            }
            if (!hasFoundNearbyEnemy)
            {
                float distanceToPlayer = Vector2.Distance(randomValidFloorTile, playerTransform.position);

                //test if enemy is not too close to the player
                if (distanceToPlayer > minEnemyDistanceFromPlayer)
                {
                    Vector3 randomFloor = new Vector3(randomValidFloorTile.x, randomValidFloorTile.y, 0);

                    colliders = Physics2D.OverlapCircleAll(randomFloor, .001f);

                    bool canSummon = true;

                    foreach (Collider2D col in colliders)
                    {
                        if (col.tag == "Obstacle")
                        {
                            canSummon = false;
                        }
                    }

                    if (canSummon)
                    {
                        Enemy spawnedEnemy = Instantiate(enemies[Random.Range(0, enemies.Length)], new Vector3(randomValidFloorTile.x, randomValidFloorTile.y, 0), Quaternion.identity);

                        enemiesSpawned++;
                    }
                }
            }

            iterations--;
            if (iterations <= 0)
            {
                Debug.LogError("BROKE SPAWN ENEMIES ITERATIONS LIMIT! ");
                break;
            }
        }

        GameManager.Instance.SetEnemyCount(enemiesSpawned);

        //not sure if I should use the composite; it messes with some collider tests
        //obstacleTilemap.GetComponent<TilemapCollider2D>().usedByComposite = true;

    }

    void ScanLevel()
    {
        if (AstarPath.active != null)
        {
            AstarPath.active.Scan();
        }
        else
        {
            Debug.LogError("No active AstarPath found in the scene");
        }
    }

    void SpawnPlayer()
    {
        foreach (var position in groundTilemap.cellBounds.allPositionsWithin)
        {
            if (groundTilemap.HasTile(position))
            {
                //Add .5, .5 because the center of tiles are at .5, .5 always
                floorTilesPositions.Add(groundTilemap.CellToWorld(position) + new Vector3(.5f, .5f, 0));
            }
        }

        for (int i = 0; i < floorTilesPositions.Count; i++)
        {
            randomValidFloorTile = floorTilesPositions[i];

            bool hasHitObstacle = false;
            Collider2D[] colliders = Physics2D.OverlapCircleAll(randomValidFloorTile, .5f);
            foreach (Collider2D collider in colliders)
            {
                if (collider.tag == "Obstacle")
                {
                    hasHitObstacle = true;
                    break;
                }
            };
            if (!hasHitObstacle)
            {
                playerTransform.position = new Vector3(randomValidFloorTile.x, randomValidFloorTile.y, 0);
                break;
            }
        }
    }

}


// [LEGACY]
//void SpawnEnemies()
//{
//    int iterations = 200;
//    while (enemiesSpawned < minNumberEnemies)
//    {
//        for (int x = 0; x < roomWidth - 1; x++)
//        {
//            for (int y = 0; y < roomHeight - 1; y++)
//            {
//                if (grid[x, y] == gridSpace.floor)
//                {
//                    SpawnEnemy(x, y);
//                }
//            }
//        }
//        iterations--;
//        if (iterations <= 0)
//        {
//            Debug.LogError("BROKE SPAWN ENEMIES ITERATIONS LIMIT! ");
//            break;
//        }
//    }

//    //originally false for testing if spawned enemies are inside walls
//    obstacleTilemap.GetComponent<TilemapCollider2D>().usedByComposite = true;

//}

//// [LEGACY]
//void SpawnEnemy(int x, int y)
//{
//    Vector2 offset = roomSizeWorldUnits / 2.0f;
//    Vector2 spawnPos = new Vector2(x, y) * worldUnitsInOneGridCell - offset;

//    Collider2D[] colliders = Physics2D.OverlapCircleAll(spawnPos, 15f);

//    foreach (Collider2D collider in colliders)
//    {
//        if (collider.tag == "Enemy")
//        {
//            if (Vector2.Distance(spawnPos, collider.transform.position) < minDistanceBetweenEnemies)
//            {
//                return;
//            }
//        }
//    }

//    float distanceToPlayer = Vector2.Distance(spawnPos, playerTransform.position);

//    if (Random.value < chanceToSpawnEnemy && enemiesSpawned < maxNumberEnemies && distanceToPlayer > minEnemyDistanceFromPlayer)
//    {
//        Enemy enemyToSpawn = enemies[Random.Range(0, enemies.Length)];
//        Enemy spawnedEnemy = Instantiate(enemyToSpawn, spawnPos, Quaternion.identity);
//        enemiesSpawned++;

//colliders = Physics2D.OverlapCircleAll(spawnedEnemy.transform.position, 1f);
//        foreach (Collider2D col in colliders)
//        {
//            if (col.tag == "Obstacle")
//            {
//                Destroy(spawnedEnemy.gameObject);
//enemiesSpawned--;
//            }
//        }
//    }
//}

//    [LEGACY]
//    void SpawnPlayer()
//{
//Vector2 offset = roomSizeWorldUnits / 2.0f;

//for (int x = Mathf.FloorToInt(-offset.x + 2); x < Mathf.FloorToInt(offset.x - 2); x++)
//{
//    for (int y = Mathf.FloorToInt(-offset.y + 2); y < Mathf.FloorToInt(offset.y - 2); y++)
//    {
//        Vector2 spawnPos = new Vector2(x, y);
//        bool hasHitObstacle = false;
//        Collider2D[] colliders = Physics2D.OverlapCircleAll(spawnPos, .6f);
//        foreach (Collider2D collider in colliders)
//        {
//            if (collider.tag == "Obstacle")
//            {
//                hasHitObstacle = true;
//                break;
//            }
//        };
//        if (!hasHitObstacle)
//        {
//            playerTransform.position = spawnPos;
//        }
//    }
//}
//}