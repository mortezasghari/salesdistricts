-- Reverse order of the Up script. Secondary assignments cascade from District.
DELETE FROM sales.Store       WHERE Id LIKE '57000000-0000-0000-0000-%';
DELETE FROM sales.District    WHERE Id LIKE 'D0000000-0000-0000-0000-%';
DELETE FROM sales.Salesperson WHERE Id LIKE '5A000000-0000-0000-0000-%';
