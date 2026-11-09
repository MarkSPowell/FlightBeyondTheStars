using UnityEngine;

namespace FBTS
{
    /// <summary>What happens on any sale, at a starport or a settlement: credits, Comms Officer experience,
    /// crew pleased by a big sale, and goodwill with the buying race (+1 standing per RepSaleValue credits).</summary>
    public static class Market
    {
        public static void Sold(GameManager gm, GameState s, string race, string what, float total)
        {
            s.credits += total;
            Crew.Gain(s, Roles.Comms, GameData.T("XpTrade", 0.5f));
            if (total >= 2000f) Crew.Morale(s, "BigSale");
            int goodwill = Mathf.FloorToInt(total / Mathf.Max(1f, GameData.T("RepSaleValue", 1000f)));
            if (goodwill > 0) s.AddRep(race, goodwill);
            gm.Say($"Sold {what} for {total:N0} cr." + (goodwill > 0 ? $" Standing with the {race} +{goodwill}." : ""));
        }

        /// <summary>Sell-artifacts section shared by starport and settlement markets (factor scales prices).</summary>
        public static void ArtifactsGui(GameManager gm, GameState s, string race, float factor)
        {
            bool any = false;
            foreach (var a in s.artifacts) if (!a.installed) { any = true; break; }
            if (!any) return;
            GUILayout.Space(10);
            GUILayout.Label("Sell artifacts", gm.Header);
            foreach (var a in new System.Collections.Generic.List<OwnedArtifact>(s.artifacts))
            {
                if (a.installed) continue;
                float price = Artifacts.SellPrice(s, s.current, a, race) * factor;
                var r = a.Row;
                bool prized = a.identified && r != null && r.PriceFor(race) > 1f;
                GUILayout.Label($"{Artifacts.Name(a)}  @ {price:N0} cr" + (prized ? "  (the " + race + " prize these)" : ""), gm.Label);
                if (a.identified && r != null && r.Is("Quest")) GUILayout.Label("Someone may want this more than any market.", gm.Hint);
                if (!a.identified) GUILayout.Label("Unidentified: identified, it may be worth far more.", gm.Hint);
                if (gm.Button("Sell", price > 0f))
                {
                    var pick = a;
                    s.artifacts.Remove(pick);
                    Sold(gm, s, race, Artifacts.Name(pick), price);
                }
            }
        }
    }
}
