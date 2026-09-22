SET client_encoding TO 'UTF8';
SET search_path = :"alkis_schema", :"parent_schema", :"postgis_schema", public;

--
-- Besondere Flurstücksgrenzen (11002)
--

CREATE TEMPORARY TABLE po_joinlines(
	ogc_fid integer PRIMARY KEY,
	gml_id character(16),
	gml_ids character(16)[],
	visited boolean,
	modell varchar[],
	adf integer[]
);
SELECT AddGeometryColumn('po_joinlines','line',:alkis_epsg,'LINESTRING',2);
CREATE INDEX po_joinlines_line ON po_joinlines USING GIST (line);
CREATE INDEX po_joinlines_visited ON po_joinlines(visited);

ANALYZE po_joinlines;

-- SELECT split_part(split_part(postgis_version(), ' ', 1), '.', 1)::int < 2 AS needlinejoin;
-- \gset
-- 
-- \if :needlinejoin
-- 
-- CREATE OR REPLACE FUNCTION pg_temp.make_line(l0 geometry, l1 geometry) RETURNS geometry AS $$
-- DECLARE
--   c RECORD;
--   r GEOMETRY := l0;
-- BEGIN
--         FOR c IN SELECT (st_dumppoints(l1)).geom
--         LOOP
--                 r := st_addpoint(r, c.geom);
--         END LOOP;
--         RETURN r;
-- END;
-- $$ LANGUAGE plpgsql IMMUTABLE;
-- 
-- \else
-- 
CREATE OR REPLACE FUNCTION pg_temp.make_line(l0 geometry, l1 geometry) RETURNS geometry AS $$
  SELECT st_makeline(l0, l1);
$$ LANGUAGE sql IMMUTABLE;

-- \endif

CREATE OR REPLACE FUNCTION pg_temp.removerepeatedpoints(geometry) RETURNS geometry AS $$
  SELECT
    st_makeline(array_agg(p))
  FROM (
    SELECT
      (g).geom AS p,
      lag((g).geom) OVER (ORDER BY (g).path) AS pp
    FROM st_dumppoints($1) AS g
  ) AS g
  WHERE pp IS NULL OR NOT st_equals(pp, p);
$$ LANGUAGE sql IMMUTABLE;

CREATE OR REPLACE FUNCTION pg_temp.alkis_besondereflurstuecksgrenze(verdraengen BOOLEAN) RETURNS varchar AS $$
DECLARE
	r0 RECORD;
	r1 RECORD;
	r2 RECORD;
	r VARCHAR;
	m VARCHAR[];
	p0 GEOMETRY;
	p1 GEOMETRY;
	l GEOMETRY;
	n INTEGER;
	np INTEGER;
	i INTEGER;
	j INTEGER;
	doneadfs INTEGER[];
	adf RECORD;
	c REFCURSOR;
	joined BOOLEAN;
BEGIN
	DELETE FROM po_lines WHERE layer='ax_besondereflurstuecksgrenze' AND signaturnummer = ANY ((SELECT DISTINCT sn FROM alkis_politischegrenzen));

	FOR adf IN SELECT sn,adfs FROM alkis_politischegrenzen g ORDER BY g.i
	LOOP
		IF verdraengen THEN
			INSERT INTO po_joinlines(ogc_fid,gml_id,gml_ids,line,visited,modell)
				SELECT ogc_fid,gml_id,gml_ids,wkb_geometry AS line,false AS visited,modell
				FROM po_besondereflurstuecksgrenze
				WHERE adf.adfs && artderflurstuecksgrenze
				  AND NOT doneadfs && artderflurstuecksgrenze;
		ELSE
			INSERT INTO po_joinlines(ogc_fid,gml_id,gml_ids,line,visited,modell)
				SELECT ogc_fid,gml_id,gml_ids,wkb_geometry AS line,false AS visited,modell
				FROM po_besondereflurstuecksgrenze
				WHERE adf.adfs && artderflurstuecksgrenze;
		END IF;

		GET DIAGNOSTICS n = ROW_COUNT;

		ANALYZE po_joinlines;

		RAISE NOTICE 'adfs:% sn:% n:%', adf.adfs, adf.sn, n;

		doneadfs := array_cat(doneadfs, adf.adfs);

		WHILE n>0
		LOOP
			SELECT ogc_fid,gml_id,gml_ids,line,modell INTO r0 FROM po_joinlines WHERE NOT visited LIMIT 1;
--			RAISE NOTICE 'START %:		von:%	nach:%)',
--						r0.ogc_fid,
--						st_astext(st_startpoint(r0.line)),
--						st_astext(st_endpoint(r0.line));
			UPDATE po_joinlines SET visited=true WHERE po_joinlines.ogc_fid=r0.ogc_fid;
			n  := n - 1;

			l := r0.line;
			m := r0.modell;

			joined := true;
			<<joined>> WHILE n>0 AND joined
			LOOP
				joined := false;

				FOR i in 0..1
				LOOP
					np := st_numpoints(l);
					p0 := st_startpoint(l);
					p1 := st_endpoint(l);

					IF st_equals(p0,p1) THEN
						EXIT joined;
					END IF;

					IF i=0 THEN
						OPEN c FOR SELECT ogc_fid,           line  AS line FROM po_joinlines WHERE p0 && line AND p0=st_endpoint(line)   AND st_equals(p0,st_endpoint(line))   AND NOT visited
						     UNION SELECT ogc_fid,st_reverse(line) AS line FROM po_joinlines WHERE p0 && line AND p0=st_startpoint(line) AND st_equals(p0,st_startpoint(line)) AND NOT visited
						     LIMIT 2;
					ELSE
						OPEN c FOR SELECT ogc_fid,           line  AS line FROM po_joinlines WHERE p1 && line AND p1=st_startpoint(line) AND st_equals(p1,st_startpoint(line)) AND NOT visited
						     UNION SELECT ogc_fid,st_reverse(line) AS line FROM po_joinlines WHERE p1 && line AND p1=st_endpoint(line)   AND st_equals(p1,st_endpoint(line))   AND NOT visited
						     LIMIT 2;
					END IF;

					FETCH c INTO r1;
					IF FOUND THEN
						FETCH c INTO r2;
						IF NOT FOUND THEN
							IF i=0 THEN
								l := pg_temp.make_line(r1.line,l);
							ELSE
								l := pg_temp.make_line(l,r1.line);
							END IF;
							IF geometrytype(l)<>'LINESTRING' OR st_numpoints(l)=np THEN
								RAISE EXCEPTION 'append failed: % with %', st_astext(l), st_astext(r1.line);
							END IF;

							UPDATE po_joinlines SET visited=true WHERE po_joinlines.ogc_fid=r1.ogc_fid;
							n  := n - 1;
							joined := true;
						END IF;
					END IF;

					CLOSE c;
				END LOOP;
			END LOOP;

			-- RAISE NOTICE 'insert line (n:%)', n;

			INSERT
				INTO po_lines(gml_id,gml_ids,thema,layer,line,signaturnummer,modell)
				VALUES (r0.gml_id,r0.gml_ids,'Politische Grenzen','ax_besondereflurstuecksgrenze',ST_CurveToLine(st_multi(pg_temp.removerepeatedpoints(l))),adf.sn,m);
		END LOOP;

		SELECT COUNT(*) INTO n FROM po_joinlines WHERE NOT visited;
		IF n>0 THEN
			RAISE NOTICE 'adf:% sn:%: % verbliebene Linien', adf.adfs, adf.sn, n;
		END IF;
		DELETE FROM po_joinlines;
	END LOOP;

	RETURN 'Politische Grenze verschmolzen';
END;
$$ LANGUAGE plpgsql;

--SELECT pg_temp.alkis_besondereflurstuecksgrenze(:alkis_pgverdraengen);
SELECT pg_temp.alkis_besondereflurstuecksgrenze(false);
