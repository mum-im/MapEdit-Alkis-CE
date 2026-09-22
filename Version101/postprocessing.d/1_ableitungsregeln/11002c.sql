SET client_encoding TO 'UTF8';
SET search_path = :"alkis_schema", :"parent_schema", :"postgis_schema", public;

--
-- Besondere Flurstücksgrenzen (11002)
-- 
-- wkb_geometry durch ST_CurveToLine(wkb_geometry,16) ersetzt
--
SELECT 'Politische Grenze werden verschmolzen';

-- TODO
CREATE TEMPORARY TABLE alkis_politischegrenzen(i INTEGER, sn VARCHAR, adfs INTEGER[]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (1, '2016', ARRAY[7101]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (2, '2018', ARRAY[7102]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (3, '2020', ARRAY[7103]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (4, '2026', ARRAY[7108]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (5, '2010', ARRAY[2500,7104]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (6, '2022', ARRAY[7106]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (7, '2024', ARRAY[7107]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (8, '2014', ARRAY[7003]);
INSERT INTO alkis_politischegrenzen(i,sn,adfs) VALUES (9, '2012', ARRAY[3000]);

CREATE TEMPORARY TABLE po_besondereflurstuecksgrenze (
	ogc_fid                 serial NOT NULL,
	gml_id                  character(16) NOT NULL,
	gml_ids                 character(16)[] NOT NULL,
	modell			varchar[],
	artderflurstuecksgrenze integer[],
	PRIMARY KEY (ogc_fid)
);

SELECT AddGeometryColumn('po_besondereflurstuecksgrenze','wkb_geometry',:alkis_epsg,'LINESTRING',2);

INSERT INTO po_besondereflurstuecksgrenze(ogc_fid,gml_id,gml_ids,modell,artderflurstuecksgrenze,wkb_geometry)
	SELECT
		min(ogc_fid),
		min(gml_id),
		array_agg(gml_id) AS gml_ids,
		ARRAY(SELECT DISTINCT unnest(alkis_accum(advstandardmodell||sonstigesmodell)) AS modell ORDER BY modell) AS modell,
		ARRAY(SELECT DISTINCT unnest(alkis_accum(artderflurstuecksgrenze)) AS artderflurstuecksgrenze ORDER BY artderflurstuecksgrenze) AS artderflurstuecksgrenze,
		ST_CurveToLine(wkb_geometry,16)
	FROM ax_besondereflurstuecksgrenze
	WHERE endet IS NULL
          AND (st_numpoints(wkb_geometry)>3 OR NOT st_equals(st_startpoint(wkb_geometry),st_endpoint(wkb_geometry)))
	GROUP BY wkb_geometry,st_asbinary(wkb_geometry);

CREATE INDEX po_besondereflurstuecksgrenze_geom_idx ON po_besondereflurstuecksgrenze USING gist (wkb_geometry);
CREATE INDEX po_besondereflurstuecksgrenze_adfg     ON po_besondereflurstuecksgrenze USING gin (artderflurstuecksgrenze);

ANALYZE po_besondereflurstuecksgrenze;
