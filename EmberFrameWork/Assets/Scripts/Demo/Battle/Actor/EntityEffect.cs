// Adapted from TEngine (Copyright (c) 2022 ALEXTANG, MIT). See ../../../FrameWork/Runtime/Core/LICENSE.txt.
using GameLogic;
using EmberFrameWork;
using UnityEngine;

public class EntityEffect : MonoBehaviour
{
	public float DelayDestroyTime = 1f;

	public void InitEntity()
	{
		Invoke(nameof(DelayDestroy), DelayDestroyTime);
	}
	private void DelayDestroy()
	{
		PoolManager.Instance.PushGameObject(this.gameObject);
	}
}
