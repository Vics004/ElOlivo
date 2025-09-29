ARCHIVO CHANGELOG

En el presente Archivo se documentarán los cambios del proyecto del sitio web El Olivo de manera estructurada con las siguientes partes para cada registro:
-	Versión: Apartado donde se mostrará la versión en la que se hizo el cambio.
-	Fecha: Apartado para mostrar la fecha del cambio con el formato “DD/MM/YYYY”
-	Descripción: Apartado donde se mencionará los cambios realizados.
-	Impacto: Apartado para mencionar las mejoras, correcciones de errores, nuevas funcionalidades o las notas operacionales.

[0.0.1] – 22/09/2025

Versión: 0.0.1 

Fecha: 22/09/2025

Descripción: Creación del repositorio con el proyecto MVC de Visual Studio con los archivos “.gitattributes” y “.gitignore” propios de GitHub. Las carpetas y archivos más importantes para mencionar serían:
-	Controllers/: carpeta donde se realizarán los controladores para cada vista.
-	Models/: carpeta donde se crearán las clases para el contexto de la base de datos, con una clase para cada tabla.
-	Views/: carpeta donde se guardarán las vistas creadas.
-	ElOlivo.sln: archivo para poder arrancar el proyecto en Visual Studio.
-	Program.cs: archivo para la configuración de los apartados importantes del sistema.
-	Appsettings.json: archivo para agregar la cadena de conexión con la base de datos.

Impacto: Se agregó el entorno de trabajo del proyecto correctamente en el repositorio para comenzar a incorporar el equipo de trabajo para el desarrollo.

[0.0.2] – 26/09/2025

Versión: 0.0.2

Fecha: 26/09/2025

Descripción: Implementación de los paquetes NuGet necesarios y se añadió la cadena conexión para poder manipular la base de datos de Supabase y utilizar el framework correctamente para la misma creación de las clases en la carpeta “Models/” donde se encontrarán el contexto y las clases del modelo de la base de datos. Algunos de los paquetes utilizados son los siguiente:
-	Npgsql.EntityFrameworkCore.PostgreSQL
-	Microsoft.EntityFrameworkCore.Design
-	Microsoft.EntityFrameworkCore.Tools

Impacto: Implementación y configuración del proyecto básica para la correcta conexión con la base de datos y, de esta forma, comenzar a desarrollar los módulos necesarios del proyecto.

[0.1.0] – 26/09/2025

Versión: 0.1.0

Fecha: 27/09/2025

Descripción: Desarrollo de las características de “inicio de sesion”, “menu lateral del usuario” y “registro del usuario” para que el usuario pueda ingresar o registrarse con las validaciones necesarias y poder navegar por las opciones en el sitio web “El Olivo”. Además, se realizaron los merges a la rama de development para realizar los cambios y pruebas necesarias para adaptar y unir las ramas de características.

Impacto: Implementación importante para que el usuario pueda incorporarse y comenzar a utilizar el sitio web para utilizar las funciones para inscribirse a un evento y mucha otra información personal.

[0.1.1] – 28/09/2025

Versión: 0.1.1 

Fecha: 28/09/2025

Descripción: Desarrollo de la característica “menu lateral del administrador” para que el usuario con el rol de administrador de eventos pueda ingresar y navegar por las opciones en el sitio web “El Olivo” para crear y gestionar eventos. Además, se realizaron los merges a la rama de development para realizar los cambios y pruebas necesarias para adaptar y unir la nueva característica.

Impacto: Implementación importante para que el administrador de eventos pueda comenzar próximamente a crear eventos y gestionarlos desde el sitio web.

