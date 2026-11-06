-- Verificación del esquema en PostgreSQL. Solo consulta datos.

-- Cargos y sus permisos.
SELECT cargo_nombre, ver, crud_requerimientos, crud_usuarios, crud_habitaciones, crud_pisos, crud_tipo_vip
FROM "Roles"
ORDER BY cargo_nombre;

-- Cuentas con cargo, y coincidencia entre Usuarios.cargo_id y los roles de Identity.
SELECT u.usuario_id, u.e_mail, r.cargo_nombre, (ur."RoleId" = u.cargo_id) AS rol_sincronizado
FROM "AspNetUsers" a
JOIN "Usuarios" u ON u.usuario_id = a."Id"
JOIN "Roles" r ON r.cargo_id = u.cargo_id
LEFT JOIN "AspNetUserRoles" ur ON ur."UserId" = a."Id"
ORDER BY r.cargo_nombre, u.e_mail;

-- Debe devolver una fila.
SELECT conname FROM pg_constraint WHERE conname = 'ex_habitacion_sin_solapes';

-- Debe devolver 0.
SELECT count(*) AS solapes_activos
FROM "Reserva Habitaciones" a
JOIN "Reserva Habitaciones" b
  ON a.habitacion_id = b.habitacion_id AND a.reserva_id < b.reserva_id
 AND a.activa AND b.activa AND a.inicio < b.fin AND a.fin > b.inicio;
