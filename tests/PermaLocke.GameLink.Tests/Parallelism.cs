using Xunit;

// Las clases de prueba de este ensamblado NO se corren en paralelo.
//
// WorldLimits es estado GLOBAL -- el techo de especies y la tabla de curvas de crecimiento del
// mundo instalado-, y GameLevelsTests lo escribe para probarlo y lo limpia al terminar. Con las
// clases en paralelo, cualquier otra que derive un nivel mientras tanto lee la tabla de otra
// prueba: PartyStatsTests, que compara el nivel guardado con el derivado de la experiencia,
// empezó a fallar sin que nada suyo hubiera cambiado, y solo dentro de la suite completa.
//
// Es la misma trampa que el §91: WorldLimits es global, y ahí costó dos diagnósticos falsos
// -«el barrido no encuentra el equipo» y «Tinkaton va dos niveles por detrás»- que resultaron ser
// la sonda leyéndolo sin haberlo puesto. Aquí sale como una prueba intermitente, que es peor,
// porque una prueba que falla una de cada tres veces acaba siendo una prueba que se ignora.
//
// El ensamblado entero tarda un segundo, así que serializarlo no cuesta nada medible. La
// alternativa -meter en una colección solo las clases que tocan WorldLimits- deja el mismo agujero
// abierto para la siguiente que lo use sin saberlo.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
