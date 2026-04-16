using UnityEngine;

namespace CannotCatchFishAlive.Components
{
    internal class DeadPickupTarget : MonoBehaviour, IHandTarget
    {
        public TechType CorpseTechType = TechType.None;
        public GameObject Root;

        public void OnHandHover(GUIHand hand)
        {
            string name = CorpseTechType != TechType.None
                ? CannotCatchFishAlive.Utils.NameUtil.GetTechTypeName(CorpseTechType)
                : (GetDisplayTechTypeName() ?? "尸体");
            var ret = HandReticle.main;
            if (ret != null)
            {
                ret.SetIcon(HandReticle.IconType.Hand, 1f);
                // 使用不翻译的文本，保留自定义中文；同时附加左手按键提示以匹配原版风格
                ret.SetInteractText("拾取", $"死亡的{name}", false, false, HandReticle.Hand.Left);
            }
        }

        public void OnHandClick(GUIHand hand)
        {
            if (Root == null) return;
            var tech = ResolveCorpseTechType();
            if (tech == TechType.None) return;
            if (CannotCatchFishAlive.Utils.ItemUtil.TryGiveDeadToPlayer(tech))
            {
                UnityEngine.Object.Destroy(Root);
            }
        }

        private string GetDisplayTechTypeName()
        {
            try
            {
                var tt = CraftData.GetTechType(Root);
                if (tt != TechType.None)
                    return CannotCatchFishAlive.Utils.NameUtil.GetTechTypeName(tt);
            }
            catch { }
            return null;
        }

        private TechType ResolveCorpseTechType()
        {
            if (CorpseTechType != TechType.None) return CorpseTechType;
            try
            {
                var tt = CraftData.GetTechType(Root);
                if (tt != TechType.None) return tt;
            }
            catch { }
            try
            {
                var tag = Root.GetComponentInChildren<TechTag>(true);
                if (tag != null && tag.type != TechType.None) return tag.type;
            }
            catch { }
            return TechType.None;
        }
    }
}
