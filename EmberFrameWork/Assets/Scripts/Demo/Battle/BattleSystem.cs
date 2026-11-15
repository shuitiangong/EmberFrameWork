// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../FrameWork/Runtime/Core/LICENSE.txt.
using Cysharp.Threading.Tasks;
using GameLogic;
using EmberFrameWork;
using UnityEngine;
using AudioType = EmberFrameWork.AudioType;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>
/// 战斗房间
/// </summary>
public class BattleSystem : Singleton<BattleSystem>, IUpdate
{
    internal const string ResourceRoot = "Assets/RawAssets/BattleDemo";
    private const string SceneLocation = ResourceRoot + "/Scenes/scene_battle.unity";
    private const string MusicLocation = ResourceRoot + "/Audios/music_background.wav";
    private bool _loadingBattle;
    private bool _released;

    private enum ESteps
    {
        None,
        Ready,
        Spawn,
        WaitSpawn,
        WaitWave,
        GameOver,
    }

    private GameObject _roomRoot;

    // 关卡参数
    private const int EnemyCount = 10;
    private const int EnemyScore = 10;
    private const int AsteroidScore = 1;
    private readonly Vector3 _spawnValues = new Vector3(6, 0, 20);

    private readonly string[] _entityLocations = new string[]
    {
        "asteroid01", "asteroid02", "asteroid03", "enemy_ship"
    };

    private ESteps _steps = ESteps.None;
    private const float StartWait = 1f;
    private const float SpawnWait = 0.75f;
    private const float WaveWait = 4f;
    private int _totalScore;
    private int _waveSpawnCount;
    private float _startWaitTimer;
    private float _spawnWaitTimer;
    private float _waveWaitTimer;

    /// <summary>
    /// 从启动流程进入战斗，或在结算后重新加载战斗场景。
    /// </summary>
    public async UniTask LoadBattleAsync()
    {
        if (_loadingBattle || _released)
            return;

        _loadingBattle = true;
        try
        {
            await GameModule.Scene.LoadSceneAsync(SceneLocation);
            if (_released)
                return;
            if (_steps != ESteps.None)
                DestroyRoom(fadeout: true);

            await LoadRoom();
        }
        finally
        {
            _loadingBattle = false;
        }
    }

    private async UniTask LoadRoom()
    {
        _totalScore = 0;
        _waveSpawnCount = 0;
        _startWaitTimer = StartWait;
        _spawnWaitTimer = SpawnWait;
        _waveWaitTimer = WaveWait;

        var window = await GameModule.UI.ShowUIAsyncAwait<UIBattleWindow>();
        if (_released)
            return;
        if (window.gameObject == null)
        {
            GameModule.UI.CloseUI<UIBattleWindow>();
            throw new GameFrameworkException("战斗界面加载失败。");
        }

        // 创建房间根对象
        _roomRoot = new GameObject("BattleRoom");

        try
        {
            GameModule.Audio.Play(AudioType.Music, MusicLocation, true);
            PoolManager.Instance.GetGameObject("player_ship", parent: _roomRoot.transform);
        }
        catch
        {
            DestroyRoom(fadeout: false);
            throw;
        }

        // 监听游戏事件
        GameEvent.AddEventListener<Vector3, Quaternion>(ActorEventDefine.PlayerDead, OnPlayerDead);
        GameEvent.AddEventListener<Vector3, Quaternion>(ActorEventDefine.EnemyDead, OnEnemyDead);
        GameEvent.AddEventListener<Vector3, Quaternion>(ActorEventDefine.AsteroidExplosion, OnAsteroidExplosion);
        GameEvent.AddEventListener<Vector3, Quaternion>(ActorEventDefine.PlayerFireBullet, OnPlayerFireBullet);
        GameEvent.AddEventListener<Vector3, Quaternion>(ActorEventDefine.EnemyFireBullet, OnEnemyFireBullet);

        _steps = ESteps.Ready;
    }

    /// <summary>
    /// 销毁房间
    /// </summary>
    private void DestroyRoom(bool fadeout)
    {
        if (_steps != ESteps.None)
        {
            _steps = ESteps.None;
            GameEvent.RemoveEventListener<Vector3, Quaternion>(ActorEventDefine.PlayerDead, OnPlayerDead);
            GameEvent.RemoveEventListener<Vector3, Quaternion>(ActorEventDefine.EnemyDead, OnEnemyDead);
            GameEvent.RemoveEventListener<Vector3, Quaternion>(ActorEventDefine.AsteroidExplosion, OnAsteroidExplosion);
            GameEvent.RemoveEventListener<Vector3, Quaternion>(ActorEventDefine.PlayerFireBullet, OnPlayerFireBullet);
            GameEvent.RemoveEventListener<Vector3, Quaternion>(ActorEventDefine.EnemyFireBullet, OnEnemyFireBullet);
        }

        if (_roomRoot != null)
            Object.Destroy(_roomRoot);
        _roomRoot = null;
        PoolManager.ClearRoomObjects();
        GameModule.Audio.Stop(AudioType.Music, fadeout);
        if (UIModule.IsValid)
            GameModule.UI.CloseUI<UIBattleWindow>();
    }

    protected override void OnRelease()
    {
        _released = true;
        if (_steps != ESteps.None)
            DestroyRoom(fadeout: false);
    }

    public void OnUpdate() => UpdateRoom();

    /// <summary>
    /// 更新房间
    /// </summary>
    private void UpdateRoom()
    {
        if (_loadingBattle || _steps == ESteps.None || _steps == ESteps.GameOver)
            return;

        if (_steps == ESteps.Ready)
        {
            _startWaitTimer -= Time.deltaTime;
            if (_startWaitTimer <= 0)
            {
                _steps = ESteps.Spawn;
            }
        }

        if (_steps == ESteps.Spawn)
        {
            var enemyLocation = _entityLocations[Random.Range(0, 4)];
            Vector3 spawnPosition = new Vector3(Random.Range(-_spawnValues.x, _spawnValues.x), _spawnValues.y, _spawnValues.z);
            Quaternion spawnRotation = Quaternion.identity;

            if (enemyLocation == "enemy_ship")
            {
                // 生成敌人实体
                var gameObject = PoolManager.Instance.GetGameObject(enemyLocation,parent: _roomRoot.transform);
                gameObject.transform.position = spawnPosition;
                gameObject.transform.rotation = spawnRotation;
                var entity = gameObject.GetComponent<EntityEnemy>();
                entity.InitEntity();
            }
            else
            {
                // 生成小行星实体
                var gameObject = PoolManager.Instance.GetGameObject(enemyLocation,parent: _roomRoot.transform);
                gameObject.transform.position = spawnPosition;
                gameObject.transform.rotation = spawnRotation;
                var entity = gameObject.GetComponent<EntityAsteroid>();
                entity.InitEntity();
            }

            _waveSpawnCount++;
            if (_waveSpawnCount >= EnemyCount)
            {
                _steps = ESteps.WaitWave;
            }
            else
            {
                _steps = ESteps.WaitSpawn;
            }
        }

        if (_steps == ESteps.WaitSpawn)
        {
            _spawnWaitTimer -= Time.deltaTime;
            if (_spawnWaitTimer <= 0)
            {
                _spawnWaitTimer = SpawnWait;
                _steps = ESteps.Spawn;
            }
        }

        if (_steps == ESteps.WaitWave)
        {
            _waveWaitTimer -= Time.deltaTime;
            if (_waveWaitTimer <= 0)
            {
                _waveWaitTimer = WaveWait;
                _waveSpawnCount = 0;
                _steps = ESteps.Spawn;
            }
        }
    }


    #region 接收事件

    private void OnPlayerDead(Vector3 position, Quaternion rotation)
    {
        if (_steps == ESteps.GameOver)
            return;

        // 创建爆炸效果
        var gameObject = PoolManager.Instance.GetGameObject("explosion_player",parent: _roomRoot.transform);
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        var entity = gameObject.GetComponent<EntityEffect>();
        entity.InitEntity();
        _steps = ESteps.GameOver;
        GameEvent.Send(ActorEventDefine.GameOver);
    }

    private void OnEnemyDead(Vector3 position, Quaternion rotation)
    {
        // 创建爆炸效果
        var gameObject = PoolManager.Instance.GetGameObject("explosion_enemy",parent: _roomRoot.transform);
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        var entity = gameObject.GetComponent<EntityEffect>();
        entity.InitEntity();

        _totalScore += EnemyScore;
        GameEvent.Send(ActorEventDefine.ScoreChange, _totalScore);
    }

    private void OnAsteroidExplosion(Vector3 position, Quaternion rotation)
    {
        // 创建爆炸效果
        var gameObject = PoolManager.Instance.GetGameObject("explosion_asteroid",parent: _roomRoot.transform);
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        var entity = gameObject.GetComponent<EntityEffect>();
        entity.InitEntity();

        _totalScore += AsteroidScore;
        GameEvent.Send(ActorEventDefine.ScoreChange, _totalScore);
    }

    private void OnPlayerFireBullet(Vector3 position, Quaternion rotation)
    {
        // 创建子弹实体
        var gameObject = PoolManager.Instance.GetGameObject("player_bullet",parent: _roomRoot.transform);
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        var entity = gameObject.GetComponent<EntityBullet>();
        entity.InitEntity();
    }

    private void OnEnemyFireBullet(Vector3 position, Quaternion rotation)
    {
        // 创建子弹实体
        var gameObject = PoolManager.Instance.GetGameObject("enemy_bullet",parent: _roomRoot.transform);
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        var entity = gameObject.GetComponent<EntityBullet>();
        entity.InitEntity();
    }

    #endregion
}
