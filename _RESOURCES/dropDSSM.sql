DELIMITER //

CREATE PROCEDURE DropDatabasesWithPrefix(IN prefix VARCHAR(100))
BEGIN
  DECLARE done INT DEFAULT FALSE;
  DECLARE db_name VARCHAR(255);
  DECLARE cur CURSOR FOR
    SELECT SCHEMA_NAME
    FROM INFORMATION_SCHEMA.SCHEMATA
    WHERE SCHEMA_NAME LIKE CONCAT(prefix, '%')
      AND SCHEMA_NAME NOT IN ('mysql', 'information_schema', 'performance_schema', 'sys');
  DECLARE CONTINUE HANDLER FOR NOT FOUND SET done = TRUE;

  OPEN cur;

  read_loop: LOOP
    FETCH cur INTO db_name;
    IF done THEN
      LEAVE read_loop;
    END IF;
    SET @drop_stmt = CONCAT('DROP DATABASE `', db_name, '`');
    PREPARE stmt FROM @drop_stmt;
    EXECUTE stmt;
    DEALLOCATE PREPARE stmt;
  END LOOP;

  CLOSE cur;
END //

DELIMITER ;

-- Call the procedure with your prefix
CALL DropDatabasesWithPrefix('dssm_');