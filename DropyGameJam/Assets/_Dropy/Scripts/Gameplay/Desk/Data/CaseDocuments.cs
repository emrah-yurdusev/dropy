using System.Globalization;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>Bir dosyanın masadaki iki kâğıdını (künye, ek rapor) vakadan üretir.</summary>
    public static class CaseDocuments
    {
        public static DocumentData Kunye(BuildingCase c)
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.title = "Proje Künyesi";
            doc.kind = DocumentKind.BasvuruFormu;
            Add(doc, "Bina adı", c.buildingName);
            Add(doc, "Kat sayısı", c.floors.ToString());
            Add(doc, "Beton (iddia)", BuildingCase.Concrete(c.claimedConcrete));
            Add(doc, "Zemin kat", c.groundUse == GroundUse.Market ? "Market" : "Konut");
            Add(doc, "Zemin kat yük.", c.groundFloorHeight.ToString("0.0#", new CultureInfo("tr-TR")) + " m");
            Add(doc, "Temel", c.foundation == Foundation.Kazik ? "Kazık" : "Radye");
            Add(doc, "Kolon", c.columns == ColumnState.Kesik ? "Kesik" : "Tam");
            Add(doc, "Perde", c.hasShearWall ? "Var" : "Yok");
            return doc;
        }

        public static DocumentData Report(BuildingCase c)
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.title = "Ek Rapor";
            doc.kind = DocumentKind.BetonRaporu;
            Add(doc, "Laboratuvar betonu", BuildingCase.Concrete(c.labConcrete));
            Add(doc, "Zemin", Soil(c.soil));
            Add(doc, "Not", string.IsNullOrEmpty(c.note) ? "—" : c.note);
            return doc;
        }

        static string Soil(SoilType soil)
        {
            switch (soil)
            {
                case SoilType.Kum: return "Kum";
                case SoilType.Kil: return "Kil";
                default: return "Kaya";
            }
        }

        static void Add(DocumentData doc, string label, string value) =>
            doc.fields.Add(new DocumentField { label = label, value = value });
    }
}
