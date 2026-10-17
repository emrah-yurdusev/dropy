using System.Globalization;
using UnityEngine;

namespace Dropy.Gameplay
{
    /// <summary>Künye ve ek rapor satırlarının anahtarları; müteahhide sorulan satırın cevabı bunlara göre üretilir.</summary>
    public static class FieldKey
    {
        public const string Name = "name", Floors = "floors", Claimed = "claimed", Use = "use", Height = "height",
            Foundation = "foundation", Columns = "columns", ShearWall = "shearwall",
            Lab = "lab", Soil = "soil", Note = "note", Hammer = "hammer";
    }

    /// <summary>Bir dosyanın masadaki iki kâğıdını (künye, ek rapor) vakadan üretir.</summary>
    public static class CaseDocuments
    {
        public static DocumentData Kunye(BuildingCase c)
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.title = "Proje Künyesi";
            doc.kind = DocumentKind.BasvuruFormu;
            Add(doc, FieldKey.Name, "Bina adı", c.buildingName);
            Add(doc, FieldKey.Floors, "Kat sayısı", c.floors.ToString());
            Add(doc, FieldKey.Claimed, "Beton (iddia)", BuildingCase.Concrete(c.claimedConcrete));
            Add(doc, FieldKey.Use, "Zemin kat", c.groundUse == GroundUse.Market ? "Market" : "Konut");
            Add(doc, FieldKey.Height, "Zemin kat yük.", c.groundFloorHeight.ToString("0.0#", new CultureInfo("tr-TR")) + " m");
            Add(doc, FieldKey.Foundation, "Temel", c.foundation == Foundation.Kazik ? "Kazık" : "Radye");
            Add(doc, FieldKey.Columns, "Kolon", c.columns == ColumnState.Kesik ? "Kesik" : "Tam");
            Add(doc, FieldKey.ShearWall, "Perde", c.hasShearWall ? "Var" : "Yok");
            return doc;
        }

        public static DocumentData Report(BuildingCase c)
        {
            var doc = ScriptableObject.CreateInstance<DocumentData>();
            doc.title = "Ek Rapor";
            doc.kind = DocumentKind.BetonRaporu;
            Add(doc, FieldKey.Lab, "Laboratuvar betonu", BuildingCase.Concrete(c.labConcrete));
            Add(doc, FieldKey.Soil, "Zemin", Soil(c.soil));
            Add(doc, FieldKey.Note, "Not", string.IsNullOrEmpty(c.note) ? "—" : c.note);
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

        static void Add(DocumentData doc, string key, string label, string value) =>
            doc.fields.Add(new DocumentField { key = key, label = label, value = value });
    }
}
