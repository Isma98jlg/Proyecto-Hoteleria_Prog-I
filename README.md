## PROJECT' DATA SOURCE
En este archi se detalla todo acerca de las dependencias informativas para el equipo de desarrollo y los agentes que trabajen sobre este proyecto.

## 🚀 Primeros pasos para ejecutar (equipo nuevo)

Requisitos:
- **.NET SDK 10** (verifica con `dotnet --list-sdks`)
- **Docker Desktop** (para la base de datos SQL Server)

Pasos:
1. Clonar el repositorio.
2. Copiar el archivo de entorno en la raíz:
   ```bash
   cp .env.example .env     # Windows: copy .env.example .env
   ```
   `.env` **no se versiona**; ahí viven las credenciales locales (contraseña SA, host, BD).
3. Levantar SQL Server (lee `.env` automáticamente):
   ```bash
   docker compose up -d
   ```
4. Ejecutar la app (el arranque aplica las migraciones EF Core + datos semilla):
   ```bash
   cd Hoteleria-App
   dotnet run
   ```
5. Abrir `http://localhost:5003` (o `https://localhost:7097`).

### ⚡ Nuevas funcionalidades de autenticación

A partir de esta versión, la aplicación incluye autenticación de personal con **ASP.NET Core Identity** en SQL Server y sesión mediante cookie segura. La cookie representa la sesión; las credenciales, roles y cuentas se guardan en la base de datos.

- **Cuenta admin por defecto:** Al primer arranque (si no existe), se crea automáticamente una cuenta con:
  - Correo: `ADMIN_EMAIL` (por defecto `admin@hoteleria.com`, definible en `.env`)
  - Contraseña: `ADMIN_PASSWORD` (por defecto `Admin123!`, definible en `.env`)
- **Cuentas de prueba:** `DEMO_WORKER_EMAIL` / `DEMO_WORKER_PASSWORD` y `DEMO_USER_EMAIL` / `DEMO_USER_PASSWORD` crean cuentas para los cargos trabajador y usuario.
- **Diagnóstico del login:** En Development, la consola del navegador muestra el usuario, cargo, roles Identity y permisos cargados después de iniciar sesión; nunca muestra la contraseña.
- **Identificador compartido:** `Usuarios.usuario_id` es la clave compartida por el perfil laboral (`Empleados`) y la cuenta (`AspNetUsers.Id`).
- **Cargos y permisos:** `Roles` es la entidad de cargos del diagrama y también la tabla de roles de Identity; conserva los permisos CRUD booleanos (`0`/`1`). `Usuarios.cargo_id` apunta al cargo asignado.
- **Acceso a la app:** Todas las páginas ahora requieren sesión iniciada (política de fallback `RequireAuthenticatedUser`). La única página pública es `/Account/Login` (y `/Error`). El header muestra el nombre del usuario logueado y un botón "Cerrar sesión".
- **Base de datos:** La migración añade las tablas `AspNet*`, `Empleados` y `Roles` a `HOTEL`. Se aplica al arrancar si `DB_AUTO_MIGRATE=true` (valor por defecto).

Todo lo relacionado con la base de datos está en `Resources/DDBB/` (esquema, script
SQL generado `hoteleria-schema.sql`, `Dockerfile` y `init-docker.sql`).

---

## Table of Contents
1. [La carpeta Resources](#1-resources-folder)

* **OTROS:**
    > [Comandos .NET utiles](#comandos-net-utiles)  
    
    > [¿Como se creo .gitignore del proyecto?](#el-gitignore) 
    
    > [Limpiar el cache del repositorio](#limpiar-el-cache)  
    
    > [Sincronizar el repositorio local después de un amend](#sincronizar-el-repo-tras-un-amend) 

    > [Sobreescribir el Rep. Remoto con el Local](#sobreescribir-el-rep-remoto-con-el-local) 


## **1. Resources Folder**
Este directorio almacena todos los detalles con respecto al diseño de la bbdd, diseño de front-end, etc. Seccionado por el nombre principal del apartado de desarrollo y posterior a ello sus demas caracteristicas:
> Por ejemplo:  
```
    Resources\DDBB
``` 
Almacena toda la informacion acerca del diseño de la BBDD, como por ejemplo un diagrama ER e incluso archivos .sql que describan al proyecto,y asi con todos los demas.
***


# **OTROS:**

<a id="comandos-net-utiles"></a>
### **Comandos .NET utiles**

* **Listar los SDK instalados en el sistema:**
    ```
        $ dotnet --list-sdks
    ```
* **Listar los Runtime instalados en el sistema:**
    ```
        $ dotnet --list-runtimes
    ```

>Para mas informacion: [Doc. de Windows](https://learn.microsoft.com/en-us/dotnet/core/install/how-to-detect-installed-versions?pivots=os-windows)

### **El .gitignore**
Este archivo se creo bajo el comando de recomendacion de microsoft para proyectos .NET, que s emuestra a continuacion.
    *Habria que darle una buena revision para ver que esta demas pero deberia ser suficiente asi como esta*

* **Comando de Windows:**
    ```
        $ dotnet new gitignore
    ```

### **Limpiar el CACHE**
El siguiente es util para cuando se anade al .gitignore archivos a ser ignorados por git que no estaban en dicho archivo previamente.

* **Comandos git:**
    ```
        $ git rm -r --cached .
        $ git add .
        $ git commit -m 'clean repository now includes the gitignore file'
        $ git push
    ```

---

<a id="sincronizar-el-repo-tras-un-amend"></a>
### **Sincronizar el repositorio local después de un `git commit --amend`**

Cuando se modifica un commit con `git commit --amend` (por ejemplo, para corregir la fecha o el mensaje del commit inicial), se reescribe el historial local. Esto hace que el historial del repositorio local ya **no esté relacionado** con el del remoto, y cualquier intento de `git push` será rechazado con el error `non-fast-forward`. De igual forma, un `git pull` normal fallará indicando que los historiales son **unrelated**.

A continuación se detalla el procedimiento completo para sincronizar el repositorio local con el remoto en este escenario:

**1. Verificar los remotes configurados**
```
    $ git remote -v
```
Este comando lista todos los repositorios remotos asociados al proyecto local junto con sus URLs de fetch y push. Es importante confirmar que el remote correcto está configurado antes de continuar.

**2. Intentar el push al remoto**
```
    $ git push rep-origin main
```
Se intenta subir los cambios locales al remoto. Este paso **fallará** con el error:
```
 ! [rejected]        main -> main (non-fast-forward)
```
Esto ocurre porque el commit local es diferente al del remoto (fue reescrito por el amend).

**3. Intentar el pull para integrar los cambios**
```
    $ git pull rep-origin main
```
Este paso también **fallará** con el error:
```
fatal: refusing to merge unrelated histories
```
Git rechaza el merge porque detecta que los historiales de ambos lados no tienen un ancestro común, debido a la reescritura del commit.

**4. Resolver la incompatibilidad de historiales**
```
    $ git pull rep-origin main --allow-unrelated-histories
```
La flag `--allow-unrelated-histories` le indica a Git que **realice el merge a pesar de que no existe un ancestro común** entre ambos historiales. Git fusionará los cambios automáticamente mediante la estrategia `ort`.

**5. Hacer el push definitivo**
```
    $ git push rep-origin main
```
Con el historial ya sincronizado, el push se realiza exitosamente.

> **Nota:** Este procedimiento solo debe aplicarse en casos donde el contenido del proyecto no ha cambiado significativamente y el amend fue únicamente cosmético (fecha, mensaje). Si se modificó contenido del proyecto, es recomendable hacer un `git reset --hard origin/main` en su lugar.

---

### **Sobreescribir el Rep. Remoto con el Local:**
Primera opcion:
```
    $ git push --force remote-repo main
```
Segunda opcion:

```
    $ git push --force-with-lease remote-repo main
```

    



