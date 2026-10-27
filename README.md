# Canelary

<p style="text-align: center;">
    <img height=250 src="./Main/Resources/canelary_512px.png" alt="yama">
    <h3 style="text-align: center;"><b><i>Muuu!</i></b></h3>
    <br/><br/>
</p>

Canelary es un entorno de desarrollo integrado para etiquetas con el lenguaje ZPL.

# ¿Cómo funciona?

Sus funcionalidades están separadas en distintos módulos independientes de dos tipos, pantallas y servicios.
Cada módulo está separado como un proyecto diferente dentro de la solución. Entre ellos se encuentran los siguientes módulos:

## Main:

Es el único módulo integrado en el ejecutable. Su principal función es cargar y mostrarle al usuario los módulos disponibles en el sistema.
Además, es el responsable de representar el estado de los puestos operacionales. Sistema relacionado con el servicio [VSTS](https://github.com/runfo-sa/canelary-servicio).

## Editor:

Es el módulo encargado en crear y modificar las etiquetas disponibles en el sistema. Además de ofrecer herramientas de ayuda para facilitar su edición.

## Comparador:

Su función consiste en permitir la comparación de dos etiquetas, ya sean dos etiquetas totalmente diferentes o dos versiones distintas de la misma.

## Verificador:

Permite a traves de un set de reglas por etiqueta, validar que todos los productos asignados a esa etiqueta estén correctamente parametrizados.

## Publicar:

Es el encargado en habilitar las etiquetas que van a estar disponibles para la producción.

# Servicios

El sistema además incluye, servicios modulares y fácilmente reemplazables, que son ampliamente utilizados por los módulos previamente mencionados.

## Backend:

Servicio encargado de proporcionar los datos necesarios para completar las variables encontradas en las etiquetas con datos reales.

Canelary incluye una implementación con el sistema PiQuatro de Twins Informática.

## Preview:

Servicio responsable de generar una previsualización digital de la etiqueta seleccionada.

Canelary incluye implementaciones con las APIs de Labelary y BinaryKits.
Para usar BinaryKits, configurar `Preview: "BinaryKits"` y
`BinaryKitsUrl: "http://localhost:8080/"` en `Settings.yaml`, y reiniciar la aplicación.
La URL puede apuntar a otra instancia de BinaryKits. Las dimensiones se convierten de pulgadas
a milímetros y se generan todas las etiquetas. Los comandos no soportados aparecen en los errores
de la preview; BinaryKits no ofrece posiciones para el resaltado del linter.
Se acepta `ProductId` en metadata `#BinaryKits` o `#Labelary`; las sustituciones de fuentes
`Languages` específicas de Labelary no se aplican en BinaryKits.

## Version:

Servicio que administra el sistema de control de versionado a utilizar. Además de permitir modificar la forma en la que se almacenan las etiquetas con total transparencia.

Canelary incluye dos implementaciones, una utilizando Git como el control de versionado y un tipo de almacenamiento por archivos tradicional. Y una utilizando una base de datos como el control de versionado y sistema de almacenamiento.
