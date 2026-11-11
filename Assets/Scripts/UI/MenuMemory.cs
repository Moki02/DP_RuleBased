// ============================================================================
// MenuMemory.cs
// What the Tab menu remembers, and what each key does, as plain C# (tested).
//
//   Tab  closed: opens on the page used last (Build the first time).   open: closes.
//   B    closed: opens on Build.    open on Build: closes.    open elsewhere: goes to Build.
//   M    closed: opens on Map.      open on Map: closes.      open elsewhere: goes to Map.
//   Esc  open: closes.
// It also remembers the last selected card on the Build page and on the Skills page.
// All of this is saved with the game (Phase 6).
//
// DESIGN RULES: R7.1 (one menu, three pages: Build, Skills, Map).
//
// HOW TO USE: GameMenu owns one and calls Press(key) / SwitchTo(page) / Select(...).
// HOW TO EDIT: nothing to tune.
// ============================================================================

using System;

namespace TheLastDrop
{
    /// <summary>The menu's three pages.</summary>
    public enum MenuPage
    {
        /// <summary>Build items (water, carry, hub storage, purification).</summary>
        Build,
        /// <summary>Skills (endurance, pathfinder, scout).</summary>
        Skills,
        /// <summary>The map.</summary>
        Map
    }

    /// <summary>The keys that drive the menu.</summary>
    public enum MenuKey
    {
        /// <summary>Open on the last page, or close.</summary>
        Tab,
        /// <summary>Open on Build, or close if already there.</summary>
        B,
        /// <summary>Open on Map, or close if already there.</summary>
        M,
        /// <summary>Close.</summary>
        Esc
    }

    /// <summary>Whether the menu is open, which page it's on, and what it remembers.</summary>
    [Serializable]
    public class MenuMemory
    {
        /// <summary>True while the menu is open.</summary>
        public bool IsOpen;
        /// <summary>The page on screen (or the last one, while closed).</summary>
        public MenuPage Current = MenuPage.Build;
        /// <summary>The page Tab opens on: the one used last. Build the first time.</summary>
        public MenuPage LastPage = MenuPage.Build;
        /// <summary>The last card selected on the Build page (id), or empty.</summary>
        public string LastBuildCard = "";
        /// <summary>The last card selected on the Skills page (id), or empty.</summary>
        public string LastSkillsCard = "";

        /// <summary>Applies a key press. Returns true if anything changed.</summary>
        public bool Press(MenuKey key)
        {
            switch (key)
            {
                case MenuKey.Tab:
                    if (IsOpen) { IsOpen = false; return true; }
                    Open(LastPage);
                    return true;
                case MenuKey.B:
                    return OpenOrClose(MenuPage.Build);
                case MenuKey.M:
                    return OpenOrClose(MenuPage.Map);
                case MenuKey.Esc:
                    if (!IsOpen) return false;
                    IsOpen = false;
                    return true;
            }
            return false;
        }

        /// <summary>Goes to a page (header tabs, "Skills ›", "‹ Build"). Opens the menu if it was closed.</summary>
        public void SwitchTo(MenuPage page) => Open(page);

        /// <summary>Remembers the selected card for its page.</summary>
        public void Select(UpgradeTab tab, string id)
        {
            if (tab == UpgradeTab.Build) LastBuildCard = id ?? "";
            else LastSkillsCard = id ?? "";
        }

        /// <summary>The remembered card for a page ("" if none).</summary>
        public string SelectedFor(UpgradeTab tab) => tab == UpgradeTab.Build ? LastBuildCard : LastSkillsCard;

        bool OpenOrClose(MenuPage page)
        {
            if (IsOpen && Current == page) { IsOpen = false; return true; }
            Open(page);
            return true;
        }

        void Open(MenuPage page)
        {
            IsOpen = true;
            Current = page;
            LastPage = page; // the page used last is the one Tab comes back to
        }
    }
}
