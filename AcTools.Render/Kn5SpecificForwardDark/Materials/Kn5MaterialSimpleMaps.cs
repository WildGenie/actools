using AcTools.Render.Base;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Shaders;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark.Materials {
    public class Kn5MaterialSimpleMaps : Kn5MaterialSimpleReflective {
        private EffectDarkMaterial.MapsMaterial _material;
        private IRenderableTexture _txNormal, _txMaps, _txDetails, _txDetailsNormal;
        private bool _hasNormalMap;

        public Kn5MaterialSimpleMaps([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            _txNormal = Kn5Material.ShaderName.Contains("damage") ? null : GetTexture("txNormal", contextHolder);
            _txMaps = GetTexture("txMaps", contextHolder);
            _txDetails = GetTexture("txDetail", contextHolder);
            _txDetailsNormal = GetTexture("txNormalDetail", contextHolder);

            if (_txNormal != null) {
                _hasNormalMap = true;
                Flags |= EffectDarkMaterial.HasNormalMap;
            }
            
            if (Equals(Kn5Material.GetPropertyValueAByName("isAdditive"), 2.0f)) {
                Flags |= EffectDarkMaterial.IsCarpaint;
            }

            if (Kn5Material.GetPropertyValueAByName("useDetail") > 0) {
                Flags |= EffectDarkMaterial.HasDetailsMap;
            }

            if (Kn5Material.ShaderName.Contains("_AT")) {
                Flags |= EffectDarkMaterial.UseNormalAlphaAsAlpha;
            }

            _material = new EffectDarkMaterial.MapsMaterial {
                DetailsUvMultipler = Kn5Material.GetPropertyValueAByName("detailUVMultiplier"),
                DetailsNormalBlend = _txDetailsNormal == null ? 0f : Kn5Material.GetPropertyValueAByName("detailNormalBlend"),
                SunSpecular = Kn5Material.GetPropertyValueAByName("sunSpecular"),
                SunSpecularExp = Kn5Material.GetPropertyValueAByName("sunSpecularEXP"),
            };

            base.Initialize(contextHolder);
        }

        public override bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (!base.Prepare(contextHolder, mode)) return false;

            Effect.FxMapsMaterial.Set(_material);

            if (_hasNormalMap && !Effect.FxNormalMap.SetResource(_txNormal)) {
                Effect.FxNormalMap.SetResource(contextHolder.GetFlatNmTexture());
            }
            
            Effect.FxDetailsMap.SetResource(_txDetails);
            Effect.FxDetailsNormalMap.SetResource(_txDetailsNormal);
            Effect.FxMapsMap.SetResource(_txMaps);
            return true;
        }

        protected override EffectTechnique GetTechnique() {
            return Effect.TechMaps;
        }

        protected override EffectTechnique GetGBufferTechnique() {
            return Effect.TechGPass_Maps;
        }
    }

    public class Kn5MaterialSimpleSkinnedMaps : Kn5MaterialSimpleMaps, ISkinnedMaterial {
        public Kn5MaterialSimpleSkinnedMaps([NotNull] Kn5MaterialDescription description) : base(description) { }

        public override void Initialize(IDeviceContextHolder contextHolder) {
            base.Initialize(contextHolder);
            InputLayout = Effect.LayoutPNTGW4B;
        }

        protected override EffectTechnique GetShadowTechnique() {
            return Effect.TechSkinnedDepthOnly;
        }

        protected override EffectTechnique GetGBufferTechnique() {
            return Effect.TechGPass_SkinnedMaps;
        }

        protected override EffectTechnique GetTechnique() {
            return Effect.TechSkinnedMaps;
        }

        void ISkinnedMaterial.SetBones(Matrix[] bones) {
            SetBones(bones);
        }
    }
}