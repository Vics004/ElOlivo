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

[0.1.5] – 04/10/2025

Versión: 0.1.5 

Fecha: 04/10/2025

Descripción: Desarrollo de las vistas iniciales del sistema tanto del menú de usuario como de administrador de eventos; se agregó parte del flujo de inscripción y el flujo de Mi Perfil, en donde el usuario podrá ver su información con las opciones de cambiar contraseña y editar su información personal, incluyendo agregar una foto de perfil, además, se implementó el servicio de Storage que ofrece Supabase para alojar archivos como imagenes.

Impacto: Ahora el usuario puede acceder a la primera vista de cada menú, permitiendo así, que pueda ver la información inicial como sus sesiones, eventos, inscripciones, certificados y su información personal. Del lado del administrador de eventos ya se han dejado las vistas para proximamente gestionar los eventos que haya creado.

[0.2.0] – 25/10/2025

Versión: 0.2.0

Fecha: 25/10/2025

Descripción: Implementación del flujo necesario para que el usuario pueda inscribirse a un Evento con la ayuda de un buscador para encontrar el evento deseado.

Impacto: Ahora el usuario puede buscar ver la información e inscribirse a los eventos.


[0.5.0] – 03/11/2025

Versión: 0.5.0

Fecha: 03/11/2025

Descripción: Función para Gestionar Usuarios en Inscripciones y Asistencias, junto con sus vistas, opciones y filtros. Además de vistas personalizadas de administrador y función de generar reportes en Excel y PDF por Inscripción y Asistencias

Impacto: Ahora los administradores de eventos pueden gestionar las Inscripciones y Asistencias para aumentar las funcionalidades y facilidades para guardar la información de los usuarios.

[0.7.0] – 06/11/2025

Versión: 0.7.0

Fecha: 06/11/2025

Descripción: Se implementó el funcionamiento de vistas y modales, para la creación de evento, sesiones, agendas y tipos de actividad, partiendo de la vista preexistente GestionEventos, rol Administrador.

Impacto: Ahora los administradores de eventos crear eventos desde una agenda interactiva que ofrece facilidades, además de la implementación de un formulario por pasos para agregar sesiones y actividades.

[0.9.0] – 09/11/2025

Versión: 0.9.0

Fecha: 09/11/2025

Descripción: Cambios de paleta de colores del calendario de ver sesiones y se añadió una nueva vista complementaria del flujo de ver sesiones, además  se reparó un bug al momento de ver inscripciones e implementación de sistema de comprobantes y certificados PDF.

Impacto: Ahora los usuarios tendrán una mejor experiencia al utilizar el sistema, además, los administradores de eventos podrán emitir los comprobantes y certificados de los eventos para que los usuarios los puedan descargar o guardar.

[1.0.0] – 18/11/2025

Versión: 1.0.0

Fecha: 18/11/2025

Descripción: Validaciones en la creación de eventos para no crear eventos para fechas pasadas, funcionalidad para editar los estados de eventos y correcciones de botones en GestionActividad para poder cancelar los modales de link y archivos. 

Impacto: Ahora los administradores de eventos no tendrán errores visuales ni de funcionalidad en los modales, además, se evitará que los usuarios puedan comenter errores de lógica.

