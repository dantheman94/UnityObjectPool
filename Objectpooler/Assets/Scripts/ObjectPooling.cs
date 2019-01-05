using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

//******************************
//
//  Created by: Daniel Marton
//
//  Last edited by: Daniel Marton
//  Last edited on: 5/1/2019
//
//******************************

/// <summary>
//  Resources used:
//  - https://gist.github.com/quill18/5a7cfffae68892621267
//  - https://catlikecoding.com/unity/tutorials/object-pools/
//  - https://docs.unity3d.com/2017.4/Documentation/Manual/UNetCustomSpawning.html
//  - https://forum.unity.com/threads/networked-object-pool-example-for-unet-hlapi.369309/
/// </summary>

////////////////////////////////////////////////////////////////////////////////////////////////////////////////

/// <summary>
//  Unity network supported object pooling class.
/// </summary>
public static class NetworkPool {

    //**************************************************************************************************************
    //                                                                                                             * 
    //      VARIABLES                                                                                              * 
    //                                                                                                             * 
    //**************************************************************************************************************

    private const int _DEFAULT_POOL_SIZE = 3;
    private static Dictionary<GameObject, Pool> _ObjectPools;

    private static Quaternion _DefaultQuat = Quaternion.identity;
    private static Vector3 _DefaultVect = Vector3.zero;

    //**************************************************************************************************************
    //                                                                                                             *
    //      FUNCTIONS                                                                                              *
    //                                                                                                             *
    //**************************************************************************************************************

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Initialize object pool dictionaries.
    /// </summary>
    // <param name="obj"></param>
    // <param name="size"></param>
    static void Init(GameObject obj = null, int size = _DEFAULT_POOL_SIZE) {

        // Create a new '_ObjectPools' dictionary if one doesnt exist
        if (_ObjectPools == null)
            _ObjectPools = new Dictionary<GameObject, Pool>();

        // If a pool for this GameObject doesnt exist yet; make one
        if (_ObjectPools != null && _ObjectPools.ContainsKey(obj) == false)
            _ObjectPools[obj] = new Pool(obj, size);        
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  If you want to preload a few copies of an object at the start
    //  of a scene, you can use this. Really not needed unless you're
    //  going to go from zero instances to 100+ very quickly.
    //  Could technically be optimized more, but in practice the
    //  Spawn/Despawn sequence is going to be pretty quick and
    //  this avoids code duplication.
    /// </summary>
    //  <param name="obj"></param>
    //  <param name="size"></param>
    static public void PreLoad(GameObject gameObject, int size) {

        // Create & initialize the pool associated for the gameObject
        Init(gameObject, size);

        // Spawn the gameObject(s) by the amount specified by 'size'
        GameObject[] objects = new GameObject[size];
        for (int i = 0; i < size; i++)
            objects[i] = ServerSpawn(gameObject, Vector3.zero, Quaternion.identity);

        // Despawn all the new objects (now they are in ready in memory for use)
        for (int i = 0; i < size; i++)
            DespawnObject(objects[i]);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Spawns a copy of the specified prefab (instantiating one if required), 
    //  NOTE: Remember that Awake() or Start() will only run on the very first
    //  spawn and that member variables won't get reset.  OnEnable will run
    //  after spawning -- but remember that toggling IsActive will also
    //  call that function.
    /// </summary>
    //  <param name="gameObject"></param>
    //  <param name="position"></param>
    //  <param name="rotation"></param>
    /// <returns>
    //  GameObject
    /// </returns>
    private static GameObject SpawnObject(GameObject gameObject, Vector3 position, Quaternion rotation) {

        // Create any dictionaries neccessary 
        Init(gameObject);

        // Spawn a pooled GameObject & return a reference to it
        return _ObjectPools[gameObject].GetFromPool(position, rotation);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Disables a GameObject and returns it back into its pool.
    /// </summary>
    //  <param name="obj"></param>
    private static void DespawnObject(GameObject obj) {

        // Despawn the object through its attached pool
        PoolMember poolMember = obj.GetComponent<PoolMember>();
        if (poolMember != null) { poolMember.LinkedPool.ReturnToPool(obj); }

        // The object wasn't spawned via an object pool, so just destroy it normally
        else { GameObject.Destroy(obj, 1f); }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Spawns a GameObject via server authority on both the server & client.
    /// </summary>
    // <param name="gameObject"></param>
    // <param name="position"></param>
    // <param name="rotation"></param>
    /// <returns>
    //  GameObject
    /// </returns>
    public static GameObject ServerSpawn(GameObject gameObject, Vector3 position, Quaternion rotation) {

        // Authority checks
        if (!NetworkServer.active) {

            Debug.LogError("ERROR: ServerSpawn has been called from a client!");
            return null;
        }

        // Get the gameObject from the pool
        var obj = SpawnObject(gameObject, position, rotation);
        if (obj == null)
            return null;

        // Spawn the gameObject on the server & client
        NetworkServer.Spawn(obj);
        return obj;
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Despawns a GameObject via server authority on both the server & client.
    /// </summary>
    // <param name="gameObject"></param>
    public static void ServerDespawn(GameObject gameObject) {

        // Authority checks
        if (!NetworkServer.active) {

            Debug.LogError("ERROR: ServerDespawn has been called from a client!");
            return;
        }

        // Despawn the gameObject on server & client
        DespawnObject(gameObject);
        NetworkServer.UnSpawn(gameObject);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    //**************************************************************************************************************
    //                                                                                                             * 
    //      CLASSES                                                                                                * 
    //                                                                                                             * 
    //**************************************************************************************************************

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Represents the object pool for a particular GameObject.
    /// </summary>
    private class Pool {

        //**************************************************************************************************************
        //                                                                                                             * 
        //      VARIABLES                                                                                              * 
        //                                                                                                             * 
        //**************************************************************************************************************

        private GameObject _GameObject;
        private GameObject _Parent;
        private NetworkHash128 _AssetID;
        private Stack<GameObject> _POOL_INACTIVE_OBJECTS;

        //**************************************************************************************************************
        //                                                                                                             *
        //      FUNCTIONS                                                                                              *
        //                                                                                                             *
        //**************************************************************************************************************

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        //  Constructor
        /// </summary>
        // <param name="obj"></param>
        // <param name="size"></param>
        public Pool(GameObject obj, int size) {

            this._GameObject = obj;
            _POOL_INACTIVE_OBJECTS = new Stack<GameObject>(size);

            _Parent = new GameObject("_OBJECT_POOL: " + obj.name);
            _Parent.transform.position = Vector3.zero;
            _Parent.transform.rotation = Quaternion.identity;

            if (_GameObject.GetComponent<NetworkIdentity>() == null) {

                Debug.LogError("ERROR: Attempted to create Network Spawn Pool but the prefab '" + _GameObject + "' does not have a NetworkIdentity.");
                return;
            }

            _AssetID = _GameObject.GetComponent<NetworkIdentity>().assetId;
            ClientScene.RegisterSpawnHandler(_AssetID, ClientSpawn, ClientDespawn);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        //  Spawn an object from the pool.
        /// </summary>
        // <param name="position"></param>
        // <param name="rotation"></param>
        /// <returns>
        //  GameObject
        /// </returns>
        public GameObject GetFromPool(Vector3 position, Quaternion rotation) {

            GameObject obj;

            // No availiable GameObjects in the pool
            if (_POOL_INACTIVE_OBJECTS.Count == 0) {

                // Create a new one
                obj = UnityEngine.Object.Instantiate(_GameObject, position, rotation);

                // Add a pool member component so we know what object pool this object belongs to
                obj.AddComponent<PoolMember>().LinkedPool = this;
            }

            // There is at least 1 availiable GameObject(s) in the pool
            else { /// (_POOL_INACTIVE_OBJECTS.Count > 0)

                // Get the last object in the array
                obj = _POOL_INACTIVE_OBJECTS.Pop();

                if (obj == null) {

                    // Somehow the object that was to be returned no longer exists 
                    // (IE: Has been destroyed or a scene change, so we try again)
                    return GetFromPool(position, rotation);
                }
            }

            // Set object's transform
            if (_Parent != null) { obj.transform.SetParent(_Parent.transform); }
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
            return obj;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////
        
        /// <summary>
        //  Returns an object back to the pool.
        /// </summary>
        // <param name="obj"></param>
        public void ReturnToPool(GameObject obj) {

            obj.SetActive(false);
            _POOL_INACTIVE_OBJECTS.Push(obj);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        //  
        /// </summary>
        // <param name="position"></param>
        // <param name="assetID"></param>
        /// <returns>
        //  GameObject
        /// </returns>
        public GameObject ClientSpawn(Vector3 position, NetworkHash128 assetID) {

            var obj = GetFromPool(position, Quaternion.identity);
            Debug.LogWarning("WARNING: Client Spawn: " + obj.GetInstanceID());
            return obj;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        //  
        /// </summary>
        // <param name="gameObject"></param>
        public static void ClientDespawn(GameObject gameObject) {

            Debug.LogWarning("WARNING: Client Despawn: " + gameObject.GetInstanceID());
            DespawnObject(gameObject);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  This is added to newly instantiated objects, so that they 
    //  can be linked back to the correct pool on despawn.
    /// </summary>
    class PoolMember : MonoBehaviour {

        public Pool LinkedPool { get; set; }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////

/// <summary>
//  Unity local machine object pooling class.
/// </summary>
public static class ObjectPooling {

    //**************************************************************************************************************
    //                                                                                                             * 
    //      VARIABLES                                                                                              * 
    //                                                                                                             * 
    //**************************************************************************************************************

    private const int _DEFAULT_POOL_SIZE = 3;
    static Dictionary<GameObject, Pool> ObjectPools;

    //**************************************************************************************************************
    //                                                                                                             *
    //      FUNCTIONS                                                                                              *
    //                                                                                                             *
    //**************************************************************************************************************

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Initialize object pool dictionaries.
    /// </summary>
    // <param name="obj"></param>
    // <param name="size"></param>
    static void Init(GameObject gameObject = null, int size = _DEFAULT_POOL_SIZE) {

        // Create a new '_ObjectPools' dictionary if one doesnt exist
        if (_ObjectPools == null)
            _ObjectPools = new Dictionary<GameObject, Pool>();

        // If a pool for this GameObject doesnt exist yet; make one
        if (_ObjectPools != null && _ObjectPools.ContainsKey(obj) == false)
            _ObjectPools[obj] = new Pool(obj, size);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  If you want to preload a few copies of an object at the start
    //  of a scene, you can use this. Really not needed unless you're
    //  going to go from zero instances to 100+ very quickly.
    //  Could technically be optimized more, but in practice the
    //  Spawn/Despawn sequence is going to be pretty darn quick and
    //  this avoids code duplication.
    /// </summary>
    // <param name="obj"></param>
    // <param name="size"></param>
    static public void PreLoad(GameObject gameObject, int size) {

        // Create & initialize the pool associated for the gameObject
        Init(gameObject, size);

        // Spawn the gameObject(s) by the amount specified by 'size'
        GameObject[] objects = new GameObject[size];
        for (int i = 0; i < size; i++)
            objects[i] = Spawn(gameObject, Vector3.zero, Quaternion.identity);

        // Despawn all the new objects (now they are in ready in memory for use)
        for (int i = 0; i < size; i++)
            DespawnObject(objects[i]);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Spawns a copy of the specified prefab (instantiating one if required).
    //  NOTE: Remember that Awake() or Start() will only run on the very first
    //  spawn and that member variables won't get reset.  OnEnable will run
    //  after spawning -- but remember that toggling IsActive will also
    //  call that function.
    /// </summary>
    // <param name="Object"></param>
    // <param name="position"></param>
    // <param name="rotation"></param>
    /// <returns>
    //  GameObject
    /// </returns>
    public static GameObject SpawnObject(GameObject gameObject, Vector3 position, Quaternion rotation) {

        // Create any dictionaries neccessary 
        Init(gameObject);

        // Spawn a pooled GameObject & return a reference to it
        return _ObjectPools[gameObject].GetFromPool(position, rotation);
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  Despawns the object and puts it back into its pool.
    /// </summary>
    // <param name="obj"></param>
    public static void Despawn(GameObject gameObject) {

        // Despawn the object through its attached pool
        PoolMember poolMember = gameObject.GetComponent<PoolMember>();
        if (poolMember != null) { poolMember.LinkedPool.ReturnToPool(gameObject); }

        // The object wasn't spawned via an object pool, so just destroy it normally
        else { GameObject.Destroy(gameObject, 1f); }
    }
    
    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    //**************************************************************************************************************
    //                                                                                                             *
    //      CLASSES                                                                                                *
    //                                                                                                             *
    //**************************************************************************************************************

    /// <summary>
    //  Represents the object pool for a particular GameObject.
    /// </summary>
    private class Pool {

        //******************************************************************************************************************************
        //
        //      VARIABLES
        //
        //******************************************************************************************************************************

        private GameObject _GameObject;
        private GameObject _Parent;
        private Stack<GameObject> _POOL_INACTIVE_OBJECTS;

        //******************************************************************************************************************************
        //
        //      FUNCTIONS
        //
        //******************************************************************************************************************************

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        //  Constructor
        /// </summary>
        // <param name="obj"></param>
        // <param name="size"></param>
        public Pool(GameObject gameObject, int size) {

            this._GameObject = gameObject;
            _POOL_INACTIVE_OBJECTS = new Stack<GameObject>(size);

            _Parent = new GameObject("_OBJECT_POOL: " + gameObject.name);
            _Parent.transform.position = Vector3.zero;
            _Parent.transform.rotation = Quaternion.identity;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        //  Spawn an object from the pool.
        /// </summary>
        // <param name="position"></param>
        // <param name="rotation"></param>
        /// <returns>
        //  GameObject
        /// </returns>
        public GameObject GetFromPool(Vector3 position, Quaternion rotation) {

            GameObject obj;

            // No availiable GameObjects in the pool
            if (_POOL_INACTIVE_OBJECTS.Count == 0) {

                // There aren't any objects in the pool to use so create a new one
                obj = GameObject.Instantiate(_GameObject, position, rotation);

                // Add a pool member component so we know what object pool this object belongs to
                obj.AddComponent<PoolMember>().LinkedPool = this;
            }

            // There is at least 1 availiable GameObject(s) in the pool
            else { /// (_POOL_INACTIVE_OBJECTS.Count > 0)

                // Get the last object in the array
                obj = _POOL_INACTIVE_OBJECTS.Pop();

                if (obj == null) {

                    // Somehow the object that was to be returned no longer exists 
                    // (IE: Has been destroyed or a scene change, so we try again)
                    return Spawn(position, rotation);
                }
            }

            // Set object's transform
            if (_Parent != null) { obj.transform.SetParent(_Parent.transform); }
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
            return obj;
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        //  Returns an object back to the pool.
        /// </summary>
        // <param name="obj"></param>
        public void ReturnToPool(GameObject gameObject) {

            gameObject.SetActive(false);
            _POOL_INACTIVE_OBJECTS.Push(gameObject);
        }

        ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    }

    ////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    /// <summary>
    //  This is added to newly instantiated objects, so that they 
    //  can be linked back to the correct pool on despawn.
    /// </summary>
    class PoolMember : MonoBehaviour {

        public Pool LinkedPool;
    }

}

////////////////////////////////////////////////////////////////////////////////////////////////////////////////