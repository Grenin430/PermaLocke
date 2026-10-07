---
tipo: investigacion
fecha: 2026-10-07
---
# Monotype en BxnnyLocke (referencia para el rol monotype de PermaLocke)

Sacado de `Locke/data/BxnnyLocke.dll` leyendo metadatos e IL (solo lectura; no se copia código ni se reutiliza nada). Las
listas de abajo son hechos de juego (qué especies admite cada rol), no código. Contexto: [[Ideas para el futuro]] ·
[[Preguntas abiertas]].

## Cómo funciona (la guardería de PermaLocke está hecha: §221)
- **El rol** es una cadena guardada con los puntos del jugador (`pts.json` y la tabla `usuarios` de su base): `monotype_agua`,
  `monotype_normal`, `monotype_bicho`, `monotype_psiquico`, `monotype_volador` (y los cuatro `tematico_*`, que van igual).
- **Lista blanca por rol** (`PoolHelper.GetLimitePorRol`): un array de números de especie. Es una lista fija a mano,
  no se calcula con los tipos. Solo cubre hasta la especie 807 (su ROM no tiene gen 8-9).
- **Validación del equipo** (`checkValidezEquipo`): recorre los 6 del equipo y las cajas desbloqueadas. Si alguna especie
  no está en la lista, avisa («Equipo pokemon no valido, no se pueden obtener nuevos pokemon. El pokemon X es invalido para
  tu rol. Liberalo o usa wondertrade.») y **no deja usar el gacha ni la guardería** hasta que no quede ninguna. Si el
  inválido es el único Pokémon y no quedan tiradas de wondertrade, regala una tirada.
- **Gacha**: sus pozos (`gachaPools/pool_N.txt`, una línea JSON por premio) se filtran quedándose solo con las especies de
  la lista (`FiltrarLineasPorLimite`).
- **Wonder trade** (`ObtenerSpeciesWondertrade`): toma el BST de la especie entregada (+1 %) y busca otra de la lista cuyo
  BST esté estrictamente entre BST±margen. Margen del 10 % al 100 % de 5 en 5 hasta que haya al menos un candidato;
  nunca devuelve la misma especie. Si ni al 100 % hay candidato, repite sin la lista (cualquier especie).
- **Baneados** (siempre fuera de wondertrade y de huevos, no del gacha ni de la validación): Mewtwo, Lugia, Ho-Oh, Slakoth,
  Vigoroth, Slaking, Kyogre, Groudon, Rayquaza, Deoxys, Dialga, Palkia, Regigigas, Giratina, Arceus, Reshiram, Zekrom,
  Kyurem, Xerneas, Yveltal, Hoopa, Cosmog, Cosmoem, Solgaleo, Lunala, Necrozma, Magearna, Zeraora.
- **GUARDERÍA** sustituye a la ruleta del ludópata en monotype y temático: botón del rol, `tiradaGuarderia` en la base
  (la concede el servidor, no la app; al pulsar gasta todas de golpe). Genera **huevos**: especie de la lista con BST
  parecido al de las del rol (`ObtenerSpeciesBSTSimilar`, con márgenes crecientes), naturaleza y habilidad al azar de
  tablas; por el código parece que el nivel o el BST objetivo dependen de las medallas Z del jugador (no lo he confirmado). Plantilla `base.pk7`.
- **Puntos**: los logros dan ×1,5 en monotype (×1,25 temático).
- No hay nada que obligue a usar solo ese tipo al jugar: solo se comprueba **lo que se posee**.

## Qué hay en las listas respecto a los tipos reales del cartucho (PKHeX USUM)
- Agua 134 (131 reales): sobran Uxie, Mesprit, Azelf, Código Cero, Silvally; faltan Kyogre y Palkia.
- Normal 113 (109 reales): sobran Mew, Regirock, Cobalion, Terrakion, Virizion, Keldeo; faltan Regigigas y Arceus.
- Bicho 81 (77 reales): sobran Mew, Shaymin, Código Cero, Silvally.
- Psíquico 76 (82 reales): faltan Mewtwo, Hoopa, Cosmog, Cosmoem, Solgaleo, Lunala.
- Volador 95 (98 reales): faltan Lugia, Ho-Oh, Rayquaza; sobra Yveltal en el lado de los baneados.
Es decir: casi igual a «el tipo, solo o doble», con arreglos a mano.

## Lista monotype_agua
Squirtle, Wartortle, Blastoise, Psyduck, Golduck, Poliwag, Poliwhirl, Poliwrath, Tentacool, Tentacruel, Slowpoke, Slowbro, Seel, Dewgong, Shellder, Cloyster, Krabby, Kingler, Horsea, Seadra, Goldeen, Seaking, Staryu, Starmie, Magikarp, Gyarados, Lapras, Vaporeon, Omanyte, Omastar, Kabuto, Kabutops, Totodile, Croconaw, Feraligatr, Chinchou, Lanturn, Marill, Azumarill, Politoed, Wooper, Quagsire, Slowking, Qwilfish, Corsola, Remoraid, Octillery, Mantine, Kingdra, Suicune, Mudkip, Marshtomp, Swampert, Lotad, Lombre, Ludicolo, Wingull, Pelipper, Surskit, Carvanha, Sharpedo, Wailmer, Wailord, Barboach, Whiscash, Corphish, Crawdaunt, Feebas, Milotic, Spheal, Sealeo, Walrein, Clamperl, Huntail, Gorebyss, Relicanth, Luvdisc, Piplup, Prinplup, Empoleon, Bibarel, Buizel, Floatzel, Shellos, Gastrodon, Finneon, Lumineon, Mantyke, Uxie, Mesprit, Azelf, Phione, Manaphy, Oshawott, Dewott, Samurott, Panpour, Simipour, Tympole, Palpitoad, Seismitoad, Basculin, Tirtouga, Carracosta, Ducklett, Swanna, Frillish, Jellicent, Alomomola, Keldeo, Froakie, Frogadier, Greninja, Binacle, Barbaracle, Skrelp, Clauncher, Clawitzer, Volcanion, Popplio, Brionne, Primarina, Wishiwashi, Mareanie, Toxapex, Dewpider, Araquanid, Wimpod, Golisopod, Pyukumuku, Código Cero, Silvally, Bruxish, Tapu Fini

## Lista monotype_normal
Pidgey, Pidgeotto, Pidgeot, Rattata, Raticate, Spearow, Fearow, Jigglypuff, Wigglytuff, Meowth, Persian, Farfetch’d, Doduo, Dodrio, Lickitung, Chansey, Kangaskhan, Tauros, Ditto, Eevee, Porygon, Snorlax, Mew, Sentret, Furret, Hoothoot, Noctowl, Igglybuff, Aipom, Girafarig, Dunsparce, Teddiursa, Ursaring, Porygon2, Stantler, Smeargle, Miltank, Blissey, Zigzagoon, Linoone, Taillow, Swellow, Slakoth, Vigoroth, Slaking, Whismur, Loudred, Exploud, Azurill, Skitty, Delcatty, Spinda, Swablu, Zangoose, Castform, Kecleon, Regirock, Starly, Staravia, Staraptor, Bidoof, Bibarel, Ambipom, Buneary, Lopunny, Glameow, Purugly, Happiny, Chatot, Munchlax, Lickilicky, Porygon-Z, Patrat, Watchog, Lillipup, Herdier, Stoutland, Pidove, Tranquill, Unfezant, Audino, Minccino, Cinccino, Deerling, Sawsbuck, Bouffalant, Rufflet, Braviary, Cobalion, Terrakion, Virizion, Keldeo, Meloetta, Bunnelby, Diggersby, Fletchling, Litleo, Pyroar, Furfrou, Helioptile, Heliolisk, Pikipek, Trumbeak, Toucannon, Yungoos, Gumshoos, Stufful, Bewear, Oranguru, Código Cero, Silvally, Komala, Drampa

## Lista monotype_bicho
Caterpie, Metapod, Butterfree, Weedle, Kakuna, Beedrill, Paras, Parasect, Venonat, Venomoth, Scyther, Pinsir, Mew, Ledyba, Ledian, Spinarak, Ariados, Yanma, Pineco, Forretress, Scizor, Shuckle, Heracross, Wurmple, Silcoon, Beautifly, Cascoon, Dustox, Surskit, Masquerain, Nincada, Ninjask, Shedinja, Volbeat, Illumise, Anorith, Armaldo, Kricketot, Kricketune, Burmy, Wormadam, Mothim, Combee, Vespiquen, Skorupi, Shaymin, Yanmega, Sewaddle, Swadloon, Leavanny, Venipede, Whirlipede, Scolipede, Dwebble, Crustle, Karrablast, Escavalier, Joltik, Galvantula, Shelmet, Accelgor, Durant, Larvesta, Volcarona, Genesect, Scatterbug, Spewpa, Vivillon, Grubbin, Charjabug, Vikavolt, Cutiefly, Ribombee, Dewpider, Araquanid, Wimpod, Golisopod, Código Cero, Silvally, Buzzwole, Pheromosa

## Lista monotype_psiquico
Abra, Kadabra, Alakazam, Slowpoke, Slowbro, Drowzee, Hypno, Exeggcute, Exeggutor, Starmie, Mr. Mime, Jynx, Mew, Natu, Xatu, Espeon, Slowking, Unown, Wobbuffet, Girafarig, Smoochum, Lugia, Celebi, Ralts, Kirlia, Gardevoir, Meditite, Medicham, Spoink, Grumpig, Lunatone, Solrock, Baltoy, Claydol, Chimecho, Wynaut, Beldum, Metang, Metagross, Latias, Latios, Jirachi, Deoxys, Chingling, Bronzor, Bronzong, Mime Jr., Gallade, Uxie, Mesprit, Azelf, Cresselia, Victini, Munna, Musharna, Woobat, Swoobat, Sigilyph, Gothita, Gothorita, Gothitelle, Solosis, Duosion, Reuniclus, Elgyem, Beheeyem, Meloetta, Delphox, Espurr, Meowstic, Inkay, Malamar, Oranguru, Bruxish, Tapu Lele, Necrozma

## Lista monotype_volador
Charizard, Butterfree, Pidgey, Pidgeotto, Pidgeot, Spearow, Fearow, Zubat, Golbat, Farfetch’d, Doduo, Dodrio, Scyther, Gyarados, Aerodactyl, Articuno, Zapdos, Moltres, Dragonite, Hoothoot, Noctowl, Ledyba, Ledian, Crobat, Togetic, Natu, Xatu, Hoppip, Skiploom, Jumpluff, Yanma, Murkrow, Gligar, Delibird, Mantine, Skarmory, Beautifly, Taillow, Swellow, Wingull, Pelipper, Masquerain, Ninjask, Swablu, Altaria, Tropius, Salamence, Starly, Staravia, Staraptor, Mothim, Combee, Vespiquen, Drifloon, Drifblim, Honchkrow, Chatot, Mantyke, Togekiss, Yanmega, Gliscor, Pidove, Tranquill, Unfezant, Woobat, Swoobat, Sigilyph, Archen, Archeops, Ducklett, Swanna, Emolga, Rufflet, Braviary, Vullaby, Mandibuzz, Tornadus, Thundurus, Landorus, Fletchling, Fletchinder, Talonflame, Vivillon, Hawlucha, Noibat, Noivern, Yveltal, Rowlet, Dartrix, Pikipek, Trumbeak, Toucannon, Oricorio, Minior, Celesteela

