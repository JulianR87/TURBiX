# Cambios

Este archivo se muestra dentro de TURBiX, en la sección «Novedades». Cada versión tiene su sección,
con los cambios relevantes contados de forma breve. Cada versión empieza con
`## X.Y.Z` (sin fecha) y cada cambio es una viñeta `- **Título.** Descripción breve.`

## 1.1.10

- **Instalación silenciosa.** `TURBiX-Setup.exe --silent` instala sin mostrar ventanas y `--uninstall --silent` desinstala sin preguntar. Sirve para instalar en varios equipos y para gestores de paquetes.
- **Arreglos tras probar en Windows 10 y 11.** Al abrir la ventana con TURBiX ya funcionando junto al reloj aparecía un aviso de «Error inesperado»; ya no pasa. En pantallas chicas la ventana ya no se sale por arriba. Las notificaciones de Windows ahora muestran el nombre TURBiX.

## 1.1.9

- **Nuevo nombre: TURBiX.** El programa, el instalador, los accesos directos y la documentación ahora se escriben TURBiX. Los archivos pasan a llamarse `TURBiX.exe` y `TURBiX-Setup.exe`; tus ajustes y copias de seguridad siguen donde estaban.

## 1.1.8

- **Documentación en seis idiomas.** La página del proyecto ahora se puede leer en los mismos idiomas que TURBiX, con más detalle sobre cada función. El programa funciona igual que en la 1.1.7.

## 1.1.7

- **Instalador.** Además del `TURBiX.exe` de siempre, ahora hay un `TURBiX-Setup.exe` que lo instala en Archivos de programa, crea el acceso directo en el menú Inicio (y en el escritorio, si quieres) y lo agrega a la lista de aplicaciones de Windows.
- **Desinstalador.** TURBiX se quita desde Configuración › Aplicaciones, como cualquier programa. Desinstalar no deshace los cambios aplicados a Windows (para eso está «Restaurar») y conserva las copias de seguridad.
- **Arreglos.** En Ajustes, «Como Windows» ahora muestra de verdad el idioma de Windows. El registro de actividad cambia de archivo cada día aunque TURBiX quede abierto. El menú del icono ya no deja iniciar una tarea ni salir mientras hay una pregunta sin responder. En «Registro de Windows», una misma entrada ya no aparece dos veces.
## 1.1.6

- **Nueva página Ajustes.** En la barra lateral, «Apariencia» pasa a ser «Ajustes». Ahí están el tema, el idioma (ahora se puede elegir a mano), el inicio con Windows, la limpieza automática de memoria, la carpeta de datos y un botón para dejar todo como viene.
- **Icono junto al reloj.** Mientras TURBiX está abierto, su icono aparece en el área de notificación. Doble clic abre la ventana; con el botón derecho se puede liberar memoria RAM, limpiar los archivos temporales o salir. Se puede ocultar desde Ajustes.
- **La X ya no cierra TURBiX.** Cerrar la ventana la esconde y TURBiX sigue funcionando junto al reloj (se puede cambiar en Ajustes). Para cerrarlo del todo está «Salir de TURBiX» en el menú del icono, que pide confirmación. Al iniciar Windows, TURBiX ahora queda directamente junto al reloj, sin abrir la ventana.

## 1.1.5

- **Notificaciones de Windows.** El aviso de memoria liberada ahora llega como notificación de Windows, con el nombre y el icono de TURBiX, en lugar de aparecer dentro del programa.
- **Abrir TURBiX al iniciar Windows.** Nueva opción en Rendimiento › Inicio de Windows (y en Memoria RAM). TURBiX se abre minimizado al iniciar sesión, para que la limpieza automática de memoria funcione siempre. Como necesita permisos de administrador, usa una tarea del Programador de tareas llamada «TURBiX», que se borra al desactivar la opción.

## 1.1.4

- **Aviso al liberar memoria.** Al limpiar la memoria RAM aparece un aviso con lo que se liberó y cuánta queda disponible. Si la limpieza automática corre con TURBiX minimizado, llega como notificación de Windows; se puede apagar en «Limpieza automática».

## 1.1.3

- **Revisado en Windows 11.** Se volvió a probar todo en un Windows 11 recién instalado: aplicar los ajustes, reiniciar y deshacerlos desde «Restaurar». Se corrigió un nombre de copia que podía verse sin traducir.
- **Pantallas chicas.** En Procesos, los nombres largos ahora se acortan con «…» en vez de cortarse, y los enlaces del Inicio ya no se cortan en ventanas angostas.

## 1.1.2

- **Novedades en tu idioma.** Con Windows en español, esta página se mostraba en inglés. Ahora sigue el mismo idioma que el resto de TURBiX.

## 1.1.1

- **TURBiX en tu idioma.** La interfaz aparece sola en el idioma de Windows: español, inglés, portugués, francés, alemán o ruso. Con cualquier otro idioma se muestra en inglés.
- **Procesos agrupados.** Nueva pestaña en Rendimiento con todos los procesos abiertos, como en el Administrador de tareas, pero con los que abrió cada programa desplegables debajo de él. Muestra procesador, memoria y subprocesos, dice qué servicio corre en cada «Host de servicio» y permite finalizar una tarea; los procesos que Windows necesita están bloqueados.
- **El registro recuerda lo anterior.** El Registro de actividad muestra también lo que TURBiX hizo en sesiones anteriores, separado por día.
- **Nombres visibles en modo oscuro.** En «Programas instalados» los nombres salían en negro sobre el fondo oscuro.

## 1.1.0

- **Nueva sección Aplicaciones.** Quita las apps que Windows instala por su cuenta (Candy Crush, Solitario con anuncios, Clipchamp, Noticias, TikTok y otras promociones) con un clic. Solo aparecen apps conocidas que se pueden quitar sin romper nada; nunca la Tienda, Xbox ni partes de Windows. Si echas alguna de menos, se reinstala gratis desde Microsoft Store.
- **Desinstalar programas.** La lista de programas instalados, con su tamaño, un buscador y un botón que abre el desinstalador oficial de cada uno.
- **Más ajustes de Windows.** Barra de tareas e Inicio (botones de Reunirse ahora, Chat, Cortana y Vista de tareas, búsqueda como icono, barra a la izquierda, archivos recientes, el acceso directo de Edge que vuelve tras cada actualización), servicios opcionales que casi nadie usa (Fax, modo tienda, mapas sin conexión, cola de impresión, indexación…) y desactivar la hibernación, que libera varios GB. Todo se revierte desde «Restaurar».
- **Copias de seguridad que no se pisan.** Si se aplicaban dos grupos de ajustes en el mismo segundo (por ejemplo, rendimiento y ajustes de Windows), la copia del primero se perdía y no se podía revertir desde «Restaurar». Ahora cada sesión tiene su propio archivo.
- **Probado también en Windows 11.** Nombres correctos en Inicio de Windows (Cortana aparecía con un código) y textos que ya no se cortan en Restaurar, Reparar y la pantalla de inicio.
- **Limpieza de Windows.old.** Borra la instalación anterior de Windows y los restos de las actualizaciones grandes, que suelen ocupar entre 10 y 30 GB, con el Liberador de espacio del propio Windows.

## 1.0.10

- **Probado a fondo en un Windows recién instalado.** Cada ajuste, la limpieza, la memoria y el inicio de Windows se aplicaron, se comprobaron tras reiniciar el equipo y se revirtieron: todo quedó guardado y la reversión devolvió el equipo exactamente a como estaba.

## 1.0.9

- **Reparar registro, más prudente.** En un Windows recién instalado marcaba como rotas unas entradas de .NET que Windows trae así de fábrica. Ahora nunca toca nada que pertenezca a la carpeta de Windows, y ya no muestra entradas repetidas.
- **Modo de juego bien detectado.** Windows lo trae activado de fábrica y TURBiX lo mostraba como «No aplicado».
- **Procesador en Salud.** Cuenta bien los núcleos en equipos con más de un procesador y muestra el modelo completo.
- **Avisos más claros.** Si no se puede crear el punto de restauración, el registro de actividad dice el motivo en lugar de quedar en blanco.

## 1.0.8

- **Iconos correctos en Windows 10.** El icono de la memoria RAM no existía en Windows 10 y se veía como un cuadro vacío; ahora se ve bien en todas las versiones.
- **Novedades sin fechas.** El historial muestra solo el número de versión.

## 1.0.7

- **Nueva sección Salud.** Muestra el estado de cada componente: discos (salud, desgaste, temperatura y horas de uso), batería, memoria RAM, procesador, tarjeta gráfica y su controlador, dispositivos con problemas y los pantallazos o apagados inesperados del último mes. Cada aviso explica qué hacer.
- **Reparar el registro de Windows.** En Soluciones › Registro de Windows: busca entradas que apuntan a programas que ya no existen (inicios rotos, programas «fantasma» en la lista de desinstalar, restos de programas borrados) y las quita guardando antes una copia que se puede restaurar.

## 1.0.6

- **Diagnóstico del equipo.** La pantalla de inicio revisa las causas más comunes de un PC lento (disco mecánico o con fallos, poco espacio, poca memoria, demasiados programas al inicio, días sin reiniciar) y te dice qué hacer con cada una.
- **Nueva sección Soluciones › Reparar.** Las reparaciones que recomienda el soporte de Microsoft, con un botón: archivos de Windows (SFC), imagen de Windows (DISM), comprobar el disco, restablecer la red, desatascar la impresora y reiniciar el Explorador.
- **Soluciones › Ajustes de Windows.** Quita molestias de Windows 11: menú contextual clásico, extensiones de archivo visibles, destacados de búsqueda, avisos de cuenta en Inicio, «Finalizar tarea» en la barra de tareas, Inicio rápido y más.
- **Limpieza más segura.** Ya no se tocan los adjuntos abiertos desde Outlook ni las carpetas ancladas en Acceso rápido, y el limpiador nunca atraviesa enlaces a otras carpetas.
- **Copias y reversión más fiables.** El valor original se guarda antes de cada cambio; si una reversión falla en parte, TURBiX lo avisa y permite reintentar.
- **Avisos más claros.** TURBiX informa si no se pudo crear el punto de restauración, y si se abrió con una cuenta de administrador distinta a la tuya.
- **Uso con teclado.** Se ve dónde está el foco al moverse con Tab y las ayudas «?» se abren con Enter.

## 1.0.5

- **Nueva sección Rendimiento.** Tres herramientas para que el equipo arranque más rápido, tenga memoria libre y rinda mejor en juegos.
- **Inicio de Windows.** Lista los programas que se abren solos al encender el equipo y permite desactivarlos con un interruptor, sin desinstalar nada.
- **Memoria RAM.** Muestra el uso en vivo y qué programas ocupan más. Un botón libera memoria al momento, y la limpieza automática la repite cada 5, 10, 15, 30 o 60 minutos.
- **Juegos.** 13 ajustes que quitan frenos de Windows al jugar: grabación en segundo plano, ahorro de energía, aceleración del ratón, programas de fondo y más. Todos se pueden revertir desde «Restaurar».

## 1.0.4

- **Historial completo.** «Novedades» vuelve a mostrar todas las versiones, sin saltear ninguna.

## 1.0.3

- **Versión instalada a la vista.** El encabezado de «Novedades» indica qué versión de TURBiX estás usando.

## 1.0.2

- **Estado más claro en el menú lateral.** Donde decía «sin conexión» ahora dice «No usa Internet», para que no parezca un problema con tu conexión. La tarjeta tiene su «?» que explica cada dato.
- **Apariencia más limpia.** Se quitó el «?» del selector de tema, que ya se entiende solo.

## 1.0.1

- **Modo claro, oscuro o el del sistema.** Nuevo selector «Apariencia» en el menú lateral. Con «Sistema», TURBiX usa el mismo modo que Windows y cambia solo cuando lo cambias en Configuración.
- **Sección Novedades.** Nueva página, antes de «Acerca de», con los cambios importantes de cada versión.

## 1.0.0

- **Primera versión.** Un único ejecutable para Windows 10 (1607 o superior) y Windows 11, sin instalación ni dependencias.
- **Privacidad y telemetría.** 23 ajustes en 5 grupos, con perfiles «Recomendado» y «Máximo» y el estado actual de cada uno.
- **Limpieza.** 20 categorías de temporales y caché, con un análisis previo de solo lectura antes de borrar nada.
- **Todo reversible.** Copia automática de cada valor modificado, reversión por sesión y punto de restauración opcional.
- **Ayuda en cada opción.** Un botón «?» explica brevemente qué hace cada ajuste.
- **Registro de actividad.** Todo lo que hace TURBiX queda anotado en pantalla y en C:\ProgramData\TURBiX\Logs.
