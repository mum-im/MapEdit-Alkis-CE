/***************************************************************************
 *                                                                         *
 * Project:  MapEdit Alkis Community Edition                               *
 * Author:   Mensch und Maschine Infrastruktur GmbH                        *
 *                                                                         *
 ***************************************************************************
 * Copyright (c) 2026, Mensch und Maschine Infrastruktur GmbH              *
 *                                                                         *
 *   This program is free software; you can redistribute it and/or modify  *
 *   it under the terms of the GNU General Public License as published by  *
 *   the Free Software Foundation; either version 2 of the License, or     *
 *   (at your option) any later version.                                   *
 *                                                                         *
 ***************************************************************************/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using Serilog;

namespace MapEdit.Alkis.Services.Structure
{
    public partial class ImportEntity : IEquatable<ImportEntity>, IComparable<ImportEntity>
    {
        public enum EntityType
        {
            UNKNOWN,
            XML,
            GZIP
        }

        internal string Profilkennung { get; set; } = string.Empty;
        internal string Datum { get; set; } = string.Empty;
        internal string Art { get; set; } = ArtBda;
        internal Boolean IsBDA { get; set; } = true;
        internal string Suedwestecke { get; set; } = string.Empty;
        internal string Antragsnummer { get; set; } = string.Empty;
        internal string Auftragsnummer { get; set; } = string.Empty;
        internal int Srid { get; set; } = -1;
        private string fqfn = string.Empty;
        public string FullyQualifiedFileName
        {
            get => fqfn;
            set => AssignName(value);
        }
        internal int Gesamt { get; set; } = -1;
        internal string MD5 { get; set; } = string.Empty;
        internal int Portion { get; set; } = -1;
        public string AlkisName { get; set; } = string.Empty;
        internal string BaseName { get; set; } = string.Empty;

        public Int32 Inserts { get; internal set; } = 0;
        public Int32 Replaces { get; internal set; } = 0;
        public Int32 Deletes { get; internal set; } = 0;
        public Int32 Updates { get; internal set; } = 0;
        internal EntityType TypeOfFile { get; set; }
        internal Boolean IsAlreadyProcessed { get; set; } = false;

        public ImportEntity()
        {
        }

        /// <summary>
        /// Sortiert BDA-Dateien nach <see cref="AlkisName"/>, NBA-Dateien nach
        /// <see cref="Datum"/>+<see cref="Portion"/>. Vergleich mit sich selbst
        /// (oder mit einer anderen Instanz, deren AlkisName ebenfalls leer ist)
        /// liefert immer 0.
        /// </summary>
        public virtual int CompareTo(ImportEntity? b)
        {
            if(b is null)
                return -1;
            if (this.IsBDA)
            {
                if (string.IsNullOrEmpty(this.AlkisName) && string.IsNullOrEmpty(b.AlkisName)) return 0;
                if (string.IsNullOrEmpty(this.AlkisName)) return -1;
                if (string.IsNullOrEmpty(b.AlkisName)) return 1;
                return string.Compare(this.AlkisName, b.AlkisName, true);
            }
            else
            {
                if (string.IsNullOrEmpty(this.Datum) && string.IsNullOrEmpty(b.Datum))
                {
                    return 0;
                }
                if (string.IsNullOrEmpty(this.Datum))
                {
                    return 1;
                }
                if (string.IsNullOrEmpty(b.Datum))
                {
                    return -1;
                }
                int ret = string.Compare(this.Datum, b.Datum, true);

                if (0 == ret)
                {
                    if ((0 <= this.Portion) || (0 <= b.Portion))
                    {
                        if (0 > this.Portion)
                        {
                            return 1;
                        }
                        if (0 > b.Portion)
                        {
                            return -1;
                        }
                        if (this.Portion < b.Portion)
                        {
                            return -1;
                        }
                        if (this.Portion > b.Portion)
                        {
                            return 1;
                        }
                    }
                    return 0;
                }
                return ret;
            }
        }

        /// <summary>
        /// Identitaet einer BDA-Datei ueber <see cref="AlkisName"/>, einer NBA-Datei
        /// ueber <see cref="Auftragsnummer"/>+<see cref="Portion"/> (Fallback:
        /// vollqualifizierter Dateiname + <see cref="Antragsnummer"/>, falls keine
        /// Auftragsnummer vorliegt). Konsistent zu <see cref="GetHashCode"/>.
        /// </summary>
        public virtual Boolean Equals(ImportEntity? b)
        {
            if(b is null)
                return false;
            if (this.IsBDA)
            {
                if (string.IsNullOrEmpty(this.AlkisName) && string.IsNullOrEmpty(b.AlkisName)) return true;
                if (string.IsNullOrEmpty(this.AlkisName) || string.IsNullOrEmpty(b.AlkisName)) return false;
                return (0 == string.Compare(this.AlkisName, b.AlkisName, true));
            }
            else
            {
                if (string.IsNullOrEmpty(this.Auftragsnummer) || string.IsNullOrEmpty(b.Auftragsnummer))
                {
                    return (0 == string.Compare(this.fqfn, b.fqfn, true)) && (0 == string.Compare(this.Antragsnummer, b.Antragsnummer, true));
                }
                int ret = string.Compare(this.Auftragsnummer, b.Auftragsnummer, true);
                if (0 == ret)
                {
                    if ((0 > this.Portion) && (0 > b.Portion))
                    {
                        return true;
                    }
                    if ((0 > this.Portion) || (0 > b.Portion))
                    {
                        return false;
                    }
                    return (this.Portion == b.Portion);
                }
                return false;
            }
        }

        public override Boolean Equals(object? obj) => Equals(obj as ImportEntity);

        public override int GetHashCode()
        {
            if (this.IsBDA)
            {
                return string.IsNullOrEmpty(this.AlkisName)
                    ? 0
                    : this.AlkisName.ToUpperInvariant().GetHashCode();
            }
            if (string.IsNullOrEmpty(this.Auftragsnummer))
            {
                return HashCode.Combine(this.fqfn.ToUpperInvariant(), this.Antragsnummer.ToUpperInvariant());
            }
            // Negative Portion-Werte gelten laut Equals() als gleich, egal welcher genaue Wert -
            // deshalb hier auf einen einzigen Bucket (-1) abgebildet statt des tatsaechlichen Werts.
            int portionBucket = this.Portion >= 0 ? this.Portion : -1;
            return HashCode.Combine(this.Auftragsnummer.ToUpperInvariant(), portionBucket);
        }

        /// <summary>
        /// Wird ueber den Setter von <see cref="FullyQualifiedFileName"/> aufgerufen.
        /// Prueft, ob die Datei existiert, und setzt bei Erfolg <c>fqfn</c>,
        /// <see cref="BaseName"/> und <see cref="AlkisName"/>; bei leerem Namen oder
        /// fehlender Datei wird nur gewarnt (kein Wurf), <c>fqfn</c> bleibt leer.
        /// </summary>
        protected void AssignName(string text1)
        {
            if(string.IsNullOrEmpty(text1)){
                Log.Warning("ImportEntity.setFileName: An empty file name was passed.");
                return;
            }
            this.fqfn = string.Empty;
            FileInfo f = new FileInfo(text1);
            if (!f.Exists)
            {
                Log.Warning("ImportEntity.setFileName: The file does not exist: " + text1);
            }
            else
            {
                this.fqfn = f.FullName;
                this.BaseName = f.Name.Split('.')[0];
                this.AlkisName = this.BaseName;
            }
        }
    }
}   

