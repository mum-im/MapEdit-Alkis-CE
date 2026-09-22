/***************************************************************************
 *                                                                         *
 * Project:  norGIS ALKIS Import                                           *
 * Purpose:  Erzeugung der Präsentationstabellen                           *
 * Author:   Jürgen E. Fischer jef@norbit.de                               *
 *                                                                         *
 ***************************************************************************
 * Copyright (c) 2013-2023 Juergen E. Fischer (jef@norbit.de)              *
 *                                                                         *
 *   This program is free software; you can redistribute it and/or modify  *
 *   it under the terms of the GNU General Public License as published by  *
 *   the Free Software Foundation; either version 2 of the License, or     *
 *   (at your option) any later version.                                   *
 *                                                                         *
 ***************************************************************************/

/***************************************************************************
 * Modified 2026-09-21 by Mensch und Maschine Infrastruktur GmbH for       *
 * MapEdit Alkis Community Edition.                                       *
 ***************************************************************************/

SET search_path = :"alkis_schema", :"postgis_schema", public;

SELECT alkis_dropobject('po_lastrun');
CREATE TABLE po_lastrun(
	lastrun character(20),
	npoints INTEGER,
	nlines INTEGER,
	npolygons INTEGER,
	nlabels INTEGER
);
INSERT INTO po_lastrun(lastrun,npoints,nlines,npolygons,nlabels) VALUES (NULL, 0, 0, 0, 0);

SELECT alkis_dropobject('po_darstellung');
CREATE TABLE po_darstellung (
  gml_id character(16),
  beginnt character(20),
  dientzurdarstellungvon character(16),
  modelle character varying[],
  art character varying,
  darstellungsprioritaet integer,
  positionierungsregel character varying,
  signaturnummer character varying
);

CREATE INDEX po_darstellung_gml_id ON po_darstellung(gml_id);
CREATE INDEX po_darstellung_dzv ON po_darstellung(dientzurdarstellungvon);
CREATE INDEX po_darstellung_art ON po_darstellung(art);

SELECT alkis_dropobject('po_ppo');
CREATE TABLE po_ppo (
  gml_id character(16),
  beginnt character(20),
  dientzurdarstellungvon character(16),
  modelle character varying[],
  art character varying,
  darstellungsprioritaet integer,
  drehwinkel double precision,
  signaturnummer character varying,
  skalierung double precision
);

SELECT AddGeometryColumn('po_ppo','wkb_geometry', :alkis_epsg, 'GEOMETRY', 2);

CREATE INDEX po_ppo_gml_id ON po_ppo(gml_id);
CREATE INDEX po_ppo_dzv ON po_ppo(dientzurdarstellungvon);
CREATE INDEX po_ppo_art ON po_ppo(art);

SELECT alkis_dropobject('po_lpo');
CREATE TABLE po_lpo (
  gml_id character(16),
  beginnt character(20),
  dientzurdarstellungvon character(16),
  modelle character varying[],
  art character varying,
  darstellungsprioritaet integer,
  signaturnummer character varying
);

SELECT AddGeometryColumn('po_lpo','wkb_geometry', :alkis_epsg, 'GEOMETRY', 2);

CREATE INDEX po_lpo_gml_id ON po_lpo(gml_id);
CREATE INDEX po_lpo_dzv ON po_lpo(dientzurdarstellungvon);
CREATE INDEX po_lpo_art ON po_lpo(art);

SELECT alkis_dropobject('po_fpo');
CREATE TABLE po_fpo (
  gml_id character(16),
  beginnt character(20),
  dientzurdarstellungvon character(16),
  modelle character varying[],
  art character varying,
  darstellungsprioritaet integer,
  signaturnummer character varying
);

SELECT AddGeometryColumn('po_fpo','wkb_geometry', :alkis_epsg, 'GEOMETRY', 2);

CREATE INDEX po_fpo_gml_id ON po_fpo(gml_id);
CREATE INDEX po_fpo_dzv ON po_fpo(dientzurdarstellungvon);
CREATE INDEX po_fpo_art ON po_fpo(art);

SELECT alkis_dropobject('po_pto');
CREATE TABLE po_pto(
  gml_id character(16),
  beginnt character(20),
  dientzurdarstellungvon character(16),
  modelle character varying[],
  art varchar,
  darstellungsprioritaet integer,
  drehwinkel double precision,
  fontsperrung double precision,
  horizontaleausrichtung character varying,
  schriftinhalt character varying,
  signaturnummer character varying,
  skalierung double precision,
  vertikaleausrichtung character varying
);

SELECT AddGeometryColumn('po_pto','wkb_geometry', :alkis_epsg, 'GEOMETRY', 2);

CREATE INDEX po_pto_gml_id ON po_pto(gml_id);
CREATE INDEX po_pto_dzv ON po_pto(dientzurdarstellungvon);
CREATE INDEX po_pto_art ON po_pto(art);

SELECT alkis_dropobject('po_lto');
CREATE TABLE po_lto (
  gml_id character(16),
  beginnt character(20),
  dientzurdarstellungvon character(16),
  modelle character varying[],
  art character varying,
  darstellungsprioritaet integer,
  fontsperrung double precision,
  horizontaleausrichtung character varying,
  schriftinhalt character varying,
  signaturnummer character varying,
  skalierung double precision,
  vertikaleausrichtung character varying
);

SELECT AddGeometryColumn('po_lto','wkb_geometry', :alkis_epsg, 'GEOMETRY', 2);

CREATE INDEX po_lto_gml_id ON po_lto(gml_id);
CREATE INDEX po_lto_dzv ON po_lto(dientzurdarstellungvon);
CREATE INDEX po_lto_art ON po_lto(art);
