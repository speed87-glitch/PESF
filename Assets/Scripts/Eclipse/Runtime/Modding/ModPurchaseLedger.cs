using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;

namespace Eclipse.Modding
{
    // Host-only purchase receipts. Inventory and balances must be saved with this
    // profile by the caller; this class neither grants items nor writes to disk.
    public sealed class ModPurchaseLedger
    {
        private const string NodeName = "EclipsePurchases";
        private const int MaximumItems = 8192;
        private sealed class ProfileGate { public readonly HashSet<DefinitionId> Pending = new HashSet<DefinitionId>(); }
        private static readonly ConditionalWeakTable<XmlElement, ProfileGate> Gates = new ConditionalWeakTable<XmlElement, ProfileGate>();
        private readonly XmlElement profile;
        private readonly ProfileGate gate;

        public readonly struct Totals
        {
            public long Transactions { get; }
            public long Units { get; }
            internal Totals(long transactions, long units) { Transactions = transactions; Units = units; }
        }

        public ModPurchaseLedger(XmlElement profile)
        {
            this.profile = profile ?? throw new ArgumentNullException(nameof(profile));
            gate = Gates.GetValue(profile, _ => new ProfileGate());
        }

        public Totals Read(DefinitionId item)
        {
            ValidateItem(item);
            lock (gate) { var entries = ReadEntries(out _); return entries.TryGetValue(item, out var totals) ? totals : default; }
        }

        // Limits concern recorded history only, never guessed pre-ledger purchases.
        // Zero is a valid deny-all limit. Null means no policy limit.
        public bool TryReserve(DefinitionId item, int quantity, long? maximumTransactions,
            long? maximumUnits, out Reservation reservation)
        {
            ValidateItem(item);
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (maximumTransactions < 0 || maximumUnits < 0) throw new ArgumentOutOfRangeException("Purchase limits must be nonnegative.");
            reservation = null;
            lock (gate)
            {
                var entries = ReadEntries(out _);
                var totals = entries.TryGetValue(item, out var found) ? found : default;
                int reservedNewItems = 0;
                foreach (var pending in gate.Pending) if (!entries.ContainsKey(pending)) reservedNewItems++;
                if (gate.Pending.Contains(item) || (!entries.ContainsKey(item) && entries.Count + reservedNewItems >= MaximumItems) ||
                    totals.Transactions == long.MaxValue || totals.Units > long.MaxValue - quantity ||
                    (maximumTransactions.HasValue && totals.Transactions >= maximumTransactions.Value) ||
                    (maximumUnits.HasValue && (quantity > maximumUnits.Value || totals.Units > maximumUnits.Value - quantity))) return false;
                gate.Pending.Add(item);
                reservation = new Reservation(this, item, quantity, totals);
                return true;
            }
        }

        public sealed class Reservation : IDisposable
        {
            private ModPurchaseLedger owner;
            private readonly DefinitionId item;
            private readonly int quantity;
            private readonly Totals before;
            internal Reservation(ModPurchaseLedger owner, DefinitionId item, int quantity, Totals before)
            { this.owner = owner; this.item = item; this.quantity = quantity; this.before = before; }

            public void Commit()
            {
                var ledger = owner;
                if (ledger == null) throw new InvalidOperationException("Purchase reservation is no longer active.");
                lock (ledger.gate)
                {
                    if (owner == null) throw new InvalidOperationException("Purchase reservation is no longer active.");
                    var entries = ledger.ReadEntries(out var oldRoot);
                    var current = entries.TryGetValue(item, out var value) ? value : default;
                    if (current.Transactions != before.Transactions || current.Units != before.Units)
                        throw new InvalidOperationException("Purchase history changed during the reservation.");
                    if (!entries.ContainsKey(item) && entries.Count >= MaximumItems)
                        throw new InvalidOperationException("Purchase history is full.");
                    entries[item] = new Totals(checked(before.Transactions + 1), checked(before.Units + quantity));
                    // Prepare a complete replacement before publishing any receipt changes.
                    var replacement = ledger.profile.OwnerDocument.CreateElement(NodeName);
                    replacement.SetAttribute("Version", "1");
                    var ids = new List<DefinitionId>(entries.Keys);
                    ids.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
                    foreach (var id in ids)
                    {
                        var entry = ledger.profile.OwnerDocument.CreateElement("Item");
                        entry.SetAttribute("Id", id.ToString());
                        entry.SetAttribute("Transactions", entries[id].Transactions.ToString(CultureInfo.InvariantCulture));
                        entry.SetAttribute("Units", entries[id].Units.ToString(CultureInfo.InvariantCulture));
                        replacement.AppendChild(entry);
                    }
                    if (oldRoot == null) ledger.profile.AppendChild(replacement);
                    else ledger.profile.ReplaceChild(replacement, oldRoot);
                    ledger.gate.Pending.Remove(item);
                    owner = null;
                }
            }

            public void Dispose()
            {
                var ledger = owner;
                if (ledger == null) return;
                lock (ledger.gate) { if (owner == null) return; owner = null; ledger.gate.Pending.Remove(item); }
            }
        }

        private Dictionary<DefinitionId, Totals> ReadEntries(out XmlElement root)
        {
            root = null;
            foreach (XmlNode child in profile.ChildNodes)
                if (child.Name == NodeName)
                {
                    if (root != null || !(child is XmlElement element)) throw new InvalidDataException("Duplicate or invalid purchase history.");
                    root = element;
                }
            var entries = new Dictionary<DefinitionId, Totals>();
            if (root == null) return entries;
            if (root.GetAttribute("Version") != "1" || root.Attributes.Count != 1) throw new InvalidDataException("Unsupported purchase history version or fields.");
            foreach (XmlNode child in root.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Whitespace || child.NodeType == XmlNodeType.Comment) continue;
                if (!(child is XmlElement entry) || entry.Name != "Item" || entry.Attributes.Count != 3 || entry.HasChildNodes ||
                    !DefinitionId.TryParse(entry.GetAttribute("Id"), out var id) || id.Category != "items" ||
                    !long.TryParse(entry.GetAttribute("Transactions"), NumberStyles.None, CultureInfo.InvariantCulture, out long transactions) ||
                    !long.TryParse(entry.GetAttribute("Units"), NumberStyles.None, CultureInfo.InvariantCulture, out long units) ||
                    transactions <= 0 || units < transactions || entries.ContainsKey(id) || entries.Count >= MaximumItems)
                    throw new InvalidDataException("Malformed purchase history entry.");
                entries.Add(id, new Totals(transactions, units));
            }
            return entries;
        }

        private static void ValidateItem(DefinitionId item)
        {
            if (item.Category != "items") throw new ArgumentException("A qualified item identity is required.", nameof(item));
        }
    }
}
