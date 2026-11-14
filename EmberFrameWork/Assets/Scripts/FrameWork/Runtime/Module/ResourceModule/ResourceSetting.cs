using System;
using UnityEngine;

namespace EmberFrameWork
{
    [CreateAssetMenu(menuName = "EmberFrameWork/ResourceSetting", fileName = "ResourceSetting")]
    public sealed class ResourceSetting : ScriptableObject
    {
        [SerializeField] private ResourceOptions options = new ResourceOptions();

        public ResourceOptions GetOptions()
        {
            if (options == null) throw new InvalidOperationException("资源配置数据为空。");
            return options.Clone();
        }

        /// <summary>程序化编辑配置。平台相关检查在应用配置或显式验证时执行。</summary>
        public void Configure(ResourceOptions value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            options = value.CreateValidatedCopy(true, false);
        }

        public bool TryValidate(bool isEditor, bool isWebGL, out string error)
        {
            try { GetOptions().CreateValidatedCopy(isEditor, isWebGL); error = null; return true; }
            catch (ArgumentException exception) { error = exception.Message; return false; }
            catch (InvalidOperationException exception) { error = exception.Message; return false; }
        }
    }
}
