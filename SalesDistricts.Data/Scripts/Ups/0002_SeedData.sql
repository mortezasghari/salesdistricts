-- Test data (Task 2). Fixed ids so the Down script can remove exactly these rows.
-- Covers the interesting cases: a district with two secondaries, one with none,
-- and one salesperson (Sofie) who is primary for two districts (transition period).

INSERT INTO sales.Salesperson (Id, Name, Email) VALUES
    ('5A000000-0000-0000-0000-000000000001', N'Anders Jensen',      N'anders.jensen@example.com'),
    ('5A000000-0000-0000-0000-000000000002', N'Mette Nielsen',      N'mette.nielsen@example.com'),
    ('5A000000-0000-0000-0000-000000000003', N'Lars Hansen',        N'lars.hansen@example.com'),
    ('5A000000-0000-0000-0000-000000000004', N'Sofie Pedersen',     N'sofie.pedersen@example.com'),
    ('5A000000-0000-0000-0000-000000000005', N'Jonas Andersen',     N'jonas.andersen@example.com'),
    ('5A000000-0000-0000-0000-000000000006', N'Freja Christensen',  NULL);

INSERT INTO sales.District (Id, Name, PrimarySalespersonId) VALUES
    ('D0000000-0000-0000-0000-000000000001', N'North Denmark',    '5A000000-0000-0000-0000-000000000001'),
    ('D0000000-0000-0000-0000-000000000002', N'Central Denmark',  '5A000000-0000-0000-0000-000000000002'),
    ('D0000000-0000-0000-0000-000000000003', N'Southern Denmark', '5A000000-0000-0000-0000-000000000003'),
    ('D0000000-0000-0000-0000-000000000004', N'Zealand',          '5A000000-0000-0000-0000-000000000004'),
    ('D0000000-0000-0000-0000-000000000005', N'Capital Region',   '5A000000-0000-0000-0000-000000000004');

INSERT INTO sales.DistrictSecondarySalesperson (DistrictId, SalespersonId) VALUES
    ('D0000000-0000-0000-0000-000000000001', '5A000000-0000-0000-0000-000000000002'),
    ('D0000000-0000-0000-0000-000000000001', '5A000000-0000-0000-0000-000000000003'),
    ('D0000000-0000-0000-0000-000000000002', '5A000000-0000-0000-0000-000000000005'),
    ('D0000000-0000-0000-0000-000000000004', '5A000000-0000-0000-0000-000000000006'),
    ('D0000000-0000-0000-0000-000000000005', '5A000000-0000-0000-0000-000000000005');

INSERT INTO sales.Store (Id, Name, Address, DistrictId) VALUES
    ('57000000-0000-0000-0000-000000000001', N'Aalborg Storcenter',   N'Hobrovej 452, 9200 Aalborg',        'D0000000-0000-0000-0000-000000000001'),
    ('57000000-0000-0000-0000-000000000002', N'Hjørring Butik',       N'Strømgade 10, 9800 Hjørring',       'D0000000-0000-0000-0000-000000000001'),
    ('57000000-0000-0000-0000-000000000003', N'Aarhus Bruuns Galleri', N'M. P. Bruuns Gade 25, 8000 Aarhus', 'D0000000-0000-0000-0000-000000000002'),
    ('57000000-0000-0000-0000-000000000004', N'Randers Storcenter',   N'Merkurvej 2, 8960 Randers',         'D0000000-0000-0000-0000-000000000002'),
    ('57000000-0000-0000-0000-000000000005', N'Odense Rosengårdcentret', N'Ørbækvej 75, 5220 Odense',       'D0000000-0000-0000-0000-000000000003'),
    ('57000000-0000-0000-0000-000000000006', N'Kolding Storcenter',   N'Skovvangen 42, 6000 Kolding',       'D0000000-0000-0000-0000-000000000003'),
    ('57000000-0000-0000-0000-000000000007', N'Roskilde Ro''s Torv',  N'Københavnsvej 65, 4000 Roskilde',   'D0000000-0000-0000-0000-000000000004'),
    ('57000000-0000-0000-0000-000000000008', N'Næstved Storcenter',   N'Næstved Storcenter 1, 4700 Næstved', 'D0000000-0000-0000-0000-000000000004'),
    ('57000000-0000-0000-0000-000000000009', N'Fields',               N'Arne Jacobsens Allé 12, 2300 København', 'D0000000-0000-0000-0000-000000000005'),
    ('57000000-0000-0000-0000-000000000010', N'Lyngby Storcenter',    N'Klampenborgvej 232, 2800 Lyngby',   'D0000000-0000-0000-0000-000000000005');
