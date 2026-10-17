using System.Collections.Generic;
using System.Linq;

namespace Dropy.Gameplay
{
    /// <summary>
    /// Oyunun sonucundan gazete sayfasını yazar: hangi manşet, hangi kutu, hangi sütun. Cümleler
    /// <see cref="NewsStyle.texts"/>'ten gelir (inspector'dan değişir); burada yalnızca seçim ve sıralama var.
    /// </summary>
    public static class NewspaperWriter
    {
        const string Good = "•  ";
        const string Bad = "×  ";

        /// <summary>6. gün depreminden sonra, hapse girilmediyse.</summary>
        public static NewspaperPage Final(IList<DeskManager.CaseResult> results, IDictionary<ShopItem, int> boughtOn,
            IEnumerable<(FamilyNeed need, bool paid)> family, bool estranged, int money, NewsStyle style)
        {
            var t = style.texts;
            var collapsed = results.Where(r => r.fate == BuildingFate.Collapsed).ToList();
            var standing = results.Where(r => r.fate == BuildingFate.Standing).ToList();
            var fellEmpty = results.Where(DeskHud.FellEmpty).ToList();
            var emptySound = results.Where(r => r.fate == BuildingFate.NotBuilt && !DeskHud.FellEmpty(r)).ToList();
            var bribes = results.Where(r => r.bribed).ToList();

            var page = new NewspaperPage { edition = string.Format(style.editionFormat, Economy.Days) };
            if (collapsed.Count > 0)
            {
                page.headline = DeskHud.Headline(collapsed[0]);
                page.subhead = Quote(collapsed[0], t);
            }
            else
            {
                page.headline = fellEmpty.Count > 0 ? string.Format(t.headlineEmptyFallen, fellEmpty.Count) : t.headlineAllStanding;
                page.subhead = fellEmpty.Count > 0 ? t.subheadEmptyFallen : t.subheadAllStanding;
            }
            page.photoCaption = bribes.Count > 0 ? t.photoCaptionBribed : t.photoCaption;

            // İkinci sütunun başı: ayakta kalan binalardan biri küçük haber olur, gerisi kutuya.
            var stories = standing.Where(r => !string.IsNullOrEmpty(r.buildingCase.standingLine)).ToList();
            if (stories.Count > 0)
            {
                page.story = stories[0].buildingCase.standingLine;
                stories.RemoveAt(0);
            }

            var home = Home(boughtOn, estranged, t);
            page.verdict = home.verdict;
            home.verdict = null;
            page.boxes.Add(home);
            if (collapsed.Count > 0)
            {
                var box = new NewsBox { title = t.boxCollapsed, column = 1 };
                foreach (var r in collapsed)
                    box.lines.Add(Bad + DeskHud.Headline(r));
                page.boxes.Add(box);
            }
            if (fellEmpty.Count > 0)
                page.boxes.Add(Box(t.boxFellEmpty, 1, Names(fellEmpty)));
            if (standing.Count > 0)
            {
                var box = new NewsBox { title = t.boxStanding, column = 1 };
                foreach (var r in stories)
                    box.lines.Add(r.buildingCase.standingLine);
                var plain = standing.Where(r => string.IsNullOrEmpty(r.buildingCase.standingLine)).ToList();
                if (plain.Count > 0)
                    box.lines.Add(Names(plain));
                if (box.lines.Count > 0)
                    page.boxes.Add(box);
            }
            if (bribes.Count > 0)
            {
                string who = string.Join(", ", bribes.Select(r => r.buildingCase.contractorName).Distinct());
                var box = Box(t.boxInvestigation, 2, string.Format(t.investigation, who));
                box.inverted = true;
                page.boxes.Add(box);
            }
            if (emptySound.Count > 0)
                page.boxes.Add(Box(t.boxEmptySound, 0, Names(emptySound), t.emptySoundNote));

            var needs = new NewsBox { title = t.boxFamily, column = 2 };
            foreach (var (need, paid) in family)
            {
                string line = paid ? need.endingPaid : need.endingUnpaid;
                if (!string.IsNullOrEmpty(line))
                    needs.lines.Add((paid ? Good : Bad) + line);
            }
            if (needs.lines.Count > 0)
                page.boxes.Add(needs);

            page.footerLeft = string.Format(t.footerReport, DeskHud.CountCorrect(results), results.Count);
            page.footerRight = money < 0 ? string.Format(t.footerDebt, Ui.Money(-money)) : string.Format(t.footerCash, Ui.Money(money));
            return page;
        }

        /// <summary>Depremde onayladığı bina çöktü: memurun tutuklandığı sayı.</summary>
        public static NewspaperPage Jail(IList<DeskManager.CaseResult> results, IList<DeskManager.CaseResult> fallen, int quakeDay, NewsStyle style)
        {
            var t = style.texts;
            var page = new NewspaperPage
            {
                edition = string.Format(t.jailEdition, quakeDay),
                headline = t.jailHeadline,
                subhead = fallen.Count == 1
                    ? string.Format(t.jailSubheadOne, fallen[0].buildingCase.buildingName)
                    : string.Format(t.jailSubheadMany, fallen.Count),
                mugshot = true,
                photoCaption = t.jailCaption,
                story = fallen.Count > 0 ? DeskHud.Headline(fallen[0]) : null,
                footerLeft = t.jailFooterLeft,
                footerRight = t.jailFooterRight,
            };

            var collapsedBox = new NewsBox { title = t.boxCollapsed, column = 1 };
            foreach (var r in fallen)
            {
                collapsedBox.lines.Add(Bad + DeskHud.Headline(r));
                collapsedBox.lines.Add("<i>" + Quote(r, t) + "</i>");
            }
            page.boxes.Add(collapsedBox);

            var court = Box(t.boxCourt, 2, t.courtText);
            if (results.Any(r => r.bribed))
                court.lines.Add(t.courtBribe);
            court.inverted = true;
            page.boxes.Add(court);
            return page;
        }

        /// <summary>Evin kutusu: iki hazırlık ve ailenin sonu. Sabitleme Elif'i korur, çanta karanlık geceyi ve yardımı.</summary>
        static NewsBox Home(IDictionary<ShopItem, int> boughtOn, bool estranged, NewspaperTexts t)
        {
            bool bag = boughtOn.ContainsKey(ShopItem.Bag);
            bool fixing = boughtOn.ContainsKey(ShopItem.Fixing);
            var box = new NewsBox { title = t.boxHome, column = 0 };
            box.lines.Add(bag ? Good + t.bagReady : Bad + t.bagNone);
            box.lines.Add(fixing ? Good + t.fixingDone : Bad + t.fixingMissing);

            if (bag && fixing)
                box.verdict = t.verdictSafe;
            else if (fixing)
                box.verdict = t.verdictNoBag;
            else
                box.verdict = bag ? t.verdictNoFixing : t.verdictNone;
            if (estranged)
                box.verdict += " " + t.estrangedHouse;
            return box;
        }

        static NewsBox Box(string title, int column, params string[] lines)
        {
            var box = new NewsBox { title = title, column = column };
            box.lines.AddRange(lines.Where(l => !string.IsNullOrEmpty(l)));
            return box;
        }

        static string Names(IEnumerable<DeskManager.CaseResult> list) => string.Join(", ", list.Select(r => r.buildingCase.buildingName));

        /// <summary>İskân günü müteahhidin söylediği söz, şimdi enkazın altına yazılır.</summary>
        static string Quote(DeskManager.CaseResult r, NewspaperTexts t) =>
            string.Format(t.contractorQuote, r.buildingCase.contractorName,
                !string.IsNullOrEmpty(r.spokenLine) ? r.spokenLine : DeskManager.SealLine(r.buildingCase, r.seal));
    }
}
