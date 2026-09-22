SET client_encoding TO 'UTF8';
SET search_path = :"alkis_schema", :"parent_schema", :"postgis_schema", public;

--
-- Besondere Flurstücksgrenzen (11002)
--

SELECT 'Besondere Flurstücksgrenzen werden verarbeitet.';

SELECT 'Strittige Flurstücksgrenzen werden verarbeitet.';

-- Strittige Grenze
INSERT INTO po_lines(gml_id,gml_ids,thema,layer,line,signaturnummer,modell)
SELECT
	o.gml_id AS gml_id,
	ARRAY[a.gml_id, b.gml_id] AS gml_ids,
	'Flurstücke' AS thema,
	'ax_besondereflurstuecksgrenze' AS layer,
	ST_CurveToLine(st_multi(o.wkb_geometry)) AS line,
	CASE
	WHEN a.abweichenderrechtszustand='true' AND b.abweichenderrechtszustand='true' THEN 2007
	ELSE 2006 END AS signaturnummer,
	coalesce(
		o.advstandardmodell||o.sonstigesmodell,
		a.advstandardmodell||a.sonstigesmodell||b.advstandardmodell||b.sonstigesmodell
	) AS modell
FROM po_lastrun, ax_besondereflurstuecksgrenze o
JOIN ax_flurstueck a ON o.wkb_geometry && a.wkb_geometry AND st_intersects(o.wkb_geometry,a.wkb_geometry) AND a.endet IS NULL
JOIN ax_flurstueck b ON o.wkb_geometry && b.wkb_geometry AND st_intersects(o.wkb_geometry,b.wkb_geometry) AND b.endet IS NULL
WHERE ARRAY[1000] <@ artderflurstuecksgrenze AND a.ogc_fid<b.ogc_fid AND o.endet IS NULL AND greatest(o.beginnt, a.beginnt, b.beginnt)>lastrun;
