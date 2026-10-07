using System.Buffers.Binary;
using Gee.External.Capstone;
using Gee.External.Capstone.Arm;
using PermaLocke.Core.Domain;
using PermaLocke.Randomizer.Rom;

namespace PermaLocke.Randomizer.Tests;

/// <summary>
/// The level cap inside the game (2026-10-06). The ARM words are written by hand, so a disassembler reads them back: a
/// wrong bit is a crash in the player's game, not a failed comparison.
/// </summary>
public sealed class RulePatchesTests
{
    private static string Disassemble(byte[] code, long address)
    {
        using var arm = CapstoneDisassembler.CreateArmDisassembler(ArmDisassembleMode.Arm);
        return string.Join("; ", arm.Disassemble(code, address).Select(i => $"{i.Mnemonic} {i.Operand}".Trim()));
    }

    [Fact]
    public void The_routine_compares_r0_with_the_cap_read_from_the_block_and_falls_back_to_100()
    {
        var routine = RulePatches.CompareWithCap();
        var code = routine[..32];

        Assert.Equal(
            "ldr ip, [pc, #0x18]; ldrb ip, [ip]; cmp ip, #0; moveq ip, #0x64; cmp ip, #0x64; movhi ip, #0x64; cmp r0, ip; bx lr",
            Disassemble(code, 0x5B9A80));

        // El literal que carga la primera instrucción: pc (+8) + 24 = el byte 32, la dirección del cap.
        Assert.Equal(RuleBlock.Cap, BinaryPrimitives.ReadUInt32LittleEndian(routine.AsSpan(32)));
    }

    [Fact]
    public void The_fallen_routine_stores_zero_for_a_listed_encryption_constant_and_what_was_asked_otherwise()
    {
        var routine = RulePatches.KeepFallenDown();

        Assert.Equal(
            "mov r5, r1; cmp r5, #0; bxeq lr; ldr ip, [r4, #8]; cmp ip, #0; bxeq lr; ldr ip, [ip]; ldr r1, [pc, #0x1c]; "
            + "mov r2, #6; ldr r3, [r1], #4; cmp r3, ip; moveq r5, #0; bxeq lr; subs r2, r2, #1; bne #0x5b9ac8; bx lr",
            Disassemble(routine[..64], RulePatches.FallenCave + 0x100000));

        // pc (+8) de la carga en el byte 28, más 28: el literal del byte 64, la lista de caídos.
        Assert.Equal(RuleBlock.Fallen, BinaryPrimitives.ReadUInt32LittleEndian(routine.AsSpan(64)));
        Assert.True(RulePatches.CompareWithCap().Length + routine.Length <= 0x5B9B40 - 0x5B9A80, "no cabe antes del código del mod");
        Assert.Equal("bl #0x5b9aa4",
            Disassemble(BitConverter.GetBytes(RulePatches.Bl(RulePatches.FallenSite, RulePatches.FallenCave)), RulePatches.FallenSite + 0x100000));
    }


    [Fact]
    public void The_dupe_test_reads_the_slot_species_bit_and_rate()
    {
        var test = RulePatches.DupeTest();

        Assert.Equal(
            "add r1, r4, sl, lsl #2; ldrh r1, [r1]; lsl r1, r1, #0x15; lsr r1, r1, #0x15; ldr ip, [pc, #0x20]; cmp r1, #0x440; "
            + "movhs r2, #0; ldrblo r2, [ip, r1, lsr #3]; andlo r1, r1, #7; lsrlo r2, r2, r1; sub r3, r4, #0xa; ldrb r3, [r3, sl]; "
            + "ands r2, r2, #1; bx lr",
            Disassemble(test[..56], RulePatches.DupeTestCave + 0x100000));

        // pc (+8) de la carga en el byte 16, más 32: el literal del byte 56.
        Assert.Equal(RuleBlock.Dupes, BinaryPrimitives.ReadUInt32LittleEndian(test.AsSpan(56)));
        Assert.Equal(RuleBlock.DupesSpeciesLimit, 0x440);
        Assert.True(RulePatches.DupeTestCave + test.Length <= 0x4B9B40, "no cabe antes del código del mod");
        Assert.True(RuleBlock.Dupes + RuleBlock.DupesBytes <= 0x6D4000, "el bloque se sale de su página");
    }

    [Fact]
    public void The_reroll_picks_again_among_the_slots_that_are_not_duplicates()
    {
        var reroll = RulePatches.RerollDupes();
        const long At = RulePatches.DupeCave + 0x100000;

        Assert.Equal(
            "push {r0, lr}; cmp r0, #0xa; bhs #0x5b9a68; mov r6, r0; mov sl, r0; bl #0x5b9ae8; beq #0x5b9a68; mov r7, #0; mov sl, #0; "
            + "bl #0x5b9ae8; addeq r7, r7, r3; add sl, sl, #1; cmp sl, #0xa; blo #0x5b9a24; movs r0, r7; moveq r0, r6; beq #0x5b9a68; "
            + "bl #0x3fbf68; mov r7, r0; mvn sl, #0; add sl, sl, #1; bl #0x5b9ae8; bne #0x5b9a50; subs r7, r7, r3; bhs #0x5b9a50; "
            + "mov r0, sl; add r6, r4, r0, lsl #2; pop {r1, pc}",
            Disassemble(reroll, At));

        Assert.True(RulePatches.DupeCave + reroll.Length <= 0x4B9A80, "pisa la rutina del cap");
        Assert.Equal("bl #0x5b9a00",
            Disassemble(BitConverter.GetBytes(RulePatches.Bl(RulePatches.DupeSite, RulePatches.DupeCave)), RulePatches.DupeSite + 0x100000));
    }


    [Fact]
    public void The_ball_refusal_takes_permalockes_reason_only_when_the_game_has_none()
    {
        var routine = RulePatches.RefuseBalls();

        Assert.Equal("ldr ip, [pc, #0x10]; ldrb ip, [ip]; cmp r4, #0; moveq r4, ip; ldr r0, [r5, #0xd8]; bx lr",
            Disassemble(routine[..24], RulePatches.BallCave));
        Assert.Equal(RuleBlock.BallRefusal, BinaryPrimitives.ReadUInt32LittleEndian(routine.AsSpan(24)));
        Assert.True(RulePatches.BallCave + routine.Length <= 0x11A7F0, "se sale de los ceros del final del segmento");
        Assert.Equal("bl #0x11a724",
            Disassemble(BitConverter.GetBytes(RulePatches.Bl(RulePatches.BallSite, RulePatches.BallCave)), RulePatches.BallSite));
    }

    [Fact]
    public void A_battle_module_with_only_the_cap_gets_the_balls_too()
    {
        if (Expansion("romfs", "Battle.cro") is not { } path) return;

        var original = File.ReadAllBytes(path);
        var bytes = (byte[])original.Clone();
        RulePatches.PatchBattle(bytes);

        // Lo que tiene hoy la carpeta de prueba: solo el cap.
        var capOnly = (byte[])original.Clone();
        bytes.AsSpan(RulePatches.ExpSite, 4).CopyTo(capOnly.AsSpan(RulePatches.ExpSite));
        bytes.AsSpan(RulePatches.BattleCave, 36).CopyTo(capOnly.AsSpan(RulePatches.BattleCave));

        Assert.Equal(RulePatches.BattleState.Patched, RulePatches.PatchBattle(capOnly));
        Assert.Equal(bytes, capOnly);
    }


    [Fact]
    public void The_spent_zone_line_goes_in_and_out_of_the_spanish_text_and_nothing_else_moves()
    {
        if (Expansion("romfs", "a", "0", "3", "6") is not { } text) return;

        var copy = Path.Combine(Path.GetTempPath(), "permalocke-texto-" + Guid.NewGuid().ToString("N"));
        File.Copy(text, copy);
        try
        {
            Assert.Equal("texto: mensaje de zona gastada puesto", RulePatches.SpentZoneText(copy, on: true));
            Assert.Null(RulePatches.SpentZoneText(copy, on: true));

            var config = new pk3DS.Core.GameConfig(pk3DS.Core.GameVersion.UM);
            var lines = pk3DS.Core.TextFile.GetStrings(config,
                new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(copy))[RulePatches.BattleMenuTextFile]);
            Assert.Equal(RulePatches.SpentZoneLineText, lines[RulePatches.SpentZoneLine]);

            Assert.Equal("texto: mensaje de Necrozma devuelto", RulePatches.SpentZoneText(copy, on: false));
            Assert.Equal(File.ReadAllBytes(text), File.ReadAllBytes(copy));
        }
        finally
        {
            File.Delete(copy);
        }
    }

    [Fact]
    public void The_calls_land_on_the_routines()
    {
        Assert.Equal("bl #0x5b9a80", Disassemble(BitConverter.GetBytes(RulePatches.Bl(0x4410E8, 0x5B9A80)), 0x4410E8));
        Assert.Equal("bl #0x11a700",
            Disassemble(BitConverter.GetBytes(RulePatches.Bl(RulePatches.ExpSite, RulePatches.BattleCave)), RulePatches.ExpSite));
    }

    [Fact]
    public void Ips_records_survive_a_round_trip_and_ours_replace_what_they_overlap()
    {
        var shiny = new IpsRecord(0x1205CF, [0xEA]);
        var old = new IpsRecord(RulePatches.CandySite, [1, 2, 3, 4]);
        var ours = new[] { new IpsRecord(RulePatches.CandySite, [9, 9, 9, 9]) };

        var merged = Ips.Merge([shiny, old], ours);
        var read = Ips.Read(Ips.Write(merged))!;

        Assert.Equal(2, read.Count);
        Assert.Contains(read, r => r.Offset == shiny.Offset && r.Bytes.SequenceEqual(shiny.Bytes));
        Assert.Contains(read, r => r.Offset == RulePatches.CandySite && r.Bytes.SequenceEqual(new byte[] { 9, 9, 9, 9 }));
        Assert.Null(Ips.Read("NOPE"u8));
    }

    [Fact]
    public void Unknown_files_are_left_alone()
    {
        Assert.Null(RulePatches.CodeRecords(new byte[0x500000]));

        var cro = new byte[0x11C974];
        Assert.Equal(RulePatches.BattleState.Unknown, RulePatches.PatchBattle(cro));
        Assert.All(cro, b => Assert.Equal(0, b));
    }

    /// <summary>The Expansion mod's own files, when this PC has them (not in the repository: they are not ours to share).</summary>
    private static string? Expansion(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var path = Path.Combine([dir.FullName, "Expansion", .. parts]);
            if (File.Exists(path)) return path;
        }

        return null;
    }

    [Fact]
    public void The_mods_battle_module_patches_and_unpatches_back_to_the_same_bytes()
    {
        if (Expansion("romfs", "Battle.cro") is not { } path) return;

        var original = File.ReadAllBytes(path);
        var bytes = (byte[])original.Clone();

        Assert.Equal(RulePatches.BattleState.Patched, RulePatches.PatchBattle(bytes));
        Assert.Equal(RulePatches.BattleState.AlreadyPatched, RulePatches.PatchBattle(bytes));
        Assert.Equal("bl #0x11a700", Disassemble(bytes[RulePatches.ExpSite..(RulePatches.ExpSite + 4)], RulePatches.ExpSite));

        // Solo cambian el sitio y la cueva.
        var changed = Enumerable.Range(0, bytes.Length).Where(i => bytes[i] != original[i]).ToList();
        Assert.All(changed, i => Assert.True(
            i is >= RulePatches.ExpSite and < RulePatches.ExpSite + 4
            || i is >= RulePatches.BallSite and < RulePatches.BallSite + 4
            || (i >= RulePatches.BattleCave && i < RulePatches.BallCave + 28)));

        Assert.True(RulePatches.UnpatchBattle(bytes));
        Assert.Equal(original, bytes);
    }

    [Fact]
    public void Applying_and_taking_away_leaves_an_installed_mod_as_it_was()
    {
        if (Expansion("exefs", "code.bin") is not { } code || Expansion("romfs", "Battle.cro") is not { } battle) return;

        var mod = Path.Combine(Path.GetTempPath(), "permalocke-reglas-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(mod, "exefs"));
        Directory.CreateDirectory(Path.Combine(mod, "romfs"));
        try
        {
            File.Copy(code, Path.Combine(mod, "exefs", "code.bin"));
            File.Copy(battle, Path.Combine(mod, "romfs", "Battle.cro"));
            // Un parche ajeno (el de todo variocolor) tiene que sobrevivir a todo.
            var shiny = Ips.Write([new IpsRecord(0x1205CF, [0xEA])]);
            File.WriteAllBytes(Path.Combine(mod, "exefs", "code.ips"), shiny);

            RulePatches.Apply(mod, on: true);
            RulePatches.Apply(mod, on: true);
            var records = Ips.Read(File.ReadAllBytes(Path.Combine(mod, "exefs", "code.ips")))!;
            Assert.Equal(8, records.Count);
            Assert.NotEqual(File.ReadAllBytes(battle), File.ReadAllBytes(Path.Combine(mod, "romfs", "Battle.cro")));

            RulePatches.Apply(mod, on: false);
            Assert.Equal(shiny, File.ReadAllBytes(Path.Combine(mod, "exefs", "code.ips")));
            Assert.Equal(File.ReadAllBytes(battle), File.ReadAllBytes(Path.Combine(mod, "romfs", "Battle.cro")));
            Assert.Equal(File.ReadAllBytes(code), File.ReadAllBytes(Path.Combine(mod, "exefs", "code.bin")));
        }
        finally
        {
            Directory.Delete(mod, true);
        }
    }

    [Fact]
    public void The_super_candy_gives_one_level_to_other_items_and_up_to_five_below_the_cap()
    {
        var a = RulePatches.SuperCandyA();
        Assert.Equal("ldr r1, [sp, #0x18]; ldrh r1, [r1]; cmp r1, #0x400; movne r1, #1; bxne lr; b #0x5b9ba8",
            Disassemble(a[..24], RulePatches.SuperCaveA + 0x100000));
        Assert.Equal(RuleBlock.Cap, BinaryPrimitives.ReadUInt32LittleEndian(a.AsSpan(24)));

        // La carga del segundo trozo cae en el literal del primero.
        var b = RulePatches.SuperCandyB();
        Assert.Equal("ldr ip, [pc, #-0x74]; ldrb r1, [ip]; subs r1, r1, r8; movle r1, #5; cmp r1, #5; b #0x5b9a70",
            Disassemble(b, RulePatches.SuperCaveB + 0x100000));
        Assert.Equal(RulePatches.SuperCaveA + 24, RulePatches.SuperCaveB + 8 - 0x74);

        // ip sigue en el cap: ocho bytes más allá, los niveles saltados.
        Assert.Equal("movgt r1, #5; sub r2, r1, #1; strb r2, [ip, #8]; bx lr",
            Disassemble(RulePatches.SuperCandyC(), RulePatches.SuperCaveC + 0x100000));
        Assert.Equal(RuleBlock.SkippedLevels, RuleBlock.Cap + 8);
        Assert.Equal("bl #0x5b9b24",
            Disassemble(BitConverter.GetBytes(RulePatches.Bl(RulePatches.SuperCandySite, RulePatches.SuperCaveA)), RulePatches.SuperCandySite + 0x100000));

        // Los tres trozos caben en sus huecos: el de la duplicada acaba en 0x4B9A70, la prueba en 0x4B9B24, el mod empieza en 0x4B9B40.
        Assert.True(RulePatches.SuperCaveC + RulePatches.SuperCandyC().Length <= RulePatches.CodeCave);
        Assert.True(RulePatches.SuperCaveA + a.Length <= 0x4B9B40);
        Assert.True(RulePatches.SuperCaveB + b.Length <= 0x4B9BC0);
        Assert.Equal(RulePatches.SuperCaveA, RulePatches.DupeTestCave + RulePatches.DupeTest().Length);
    }

    [Fact]
    public void The_move_check_takes_the_skipped_levels_and_forgets_them_when_it_is_done()
    {
        var skipped = RulePatches.LearnSkipped();
        Assert.Equal("ldr r2, [pc, #0x10]; ldrb r2, [r2]; sub r3, r7, r2; cmp r8, r3; bxhs lr; b #0x3257b0",
            Disassemble(skipped[..24], RulePatches.LearnCave + 0x100000));
        Assert.Equal(RuleBlock.SkippedLevels, BinaryPrimitives.ReadUInt32LittleEndian(skipped.AsSpan(24)));

        var done = RulePatches.LearnDone();
        Assert.Equal("ldr r1, [pc, #0xc]; mov r0, #0; strb r0, [r1]; mov r0, #3; bx lr",
            Disassemble(done[..20], RulePatches.LearnDoneCave + 0x100000));
        Assert.Equal(RuleBlock.SkippedLevels, BinaryPrimitives.ReadUInt32LittleEndian(done.AsSpan(20)));

        // Cada rutina cabe en su assert vacío (siete palabras desde el segundo, sin la guardia ni su salto).
        Assert.True(skipped.Length <= 28 && done.Length <= 28);
        Assert.Equal("blne #0x325678",
            Disassemble(BitConverter.GetBytes(RulePatches.B(RulePatches.LearnSite, RulePatches.LearnCave, cond: 1, link: true)), RulePatches.LearnSite + 0x100000));
    }

    [Fact]
    public void The_super_candy_goes_into_the_mods_files_once_and_its_code_points_at_its_icon()
    {
        if (Expansion("romfs", "a", "0", "1", "9") is not { } data || Expansion("romfs", "a", "0", "3", "6") is not { } text
            || Expansion("romfs", "a", "0", "6", "1") is not { } icons || Expansion("exefs", "code.bin") is not { } code) return;

        var romfs = Path.Combine(Path.GetTempPath(), "permalocke-super-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var (from, parts) in new[] { (data, "1/9"), (text, "3/6"), (icons, "6/1") })
            {
                var to = Path.Combine([romfs, "a", "0", .. parts.Split('/')]);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to);
            }

            var before = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(icons)).FileCount;
            var icon = RulePatches.InstallSuperCandy(romfs, out var problem);
            Assert.Null(problem);
            Assert.Equal(before, icon);
            Assert.Equal(icon, RulePatches.InstallSuperCandy(romfs, out problem));
            Assert.Null(problem);

            var items = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "1", "9")));
            Assert.Equal(RulePatches.SuperCandyPrice, BinaryPrimitives.ReadUInt16LittleEndian(items[SuperCandy.ItemId]));
            Assert.Equal(items[RulePatches.RareCandyItem][2..], items[SuperCandy.ItemId][2..]);

            var config = new pk3DS.Core.GameConfig(pk3DS.Core.GameVersion.UM);
            var garc = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "3", "6")));
            string Line(int file) => pk3DS.Core.TextFile.GetStrings(config, garc[file])[SuperCandy.ItemId];
            Assert.Equal(SuperCandy.Name, Line(RulePatches.ItemNamesFile));
            Assert.Equal(SuperCandy.Name + "s", Line(RulePatches.ItemPluralFile));
            Assert.Equal(SuperCandy.Name + "[VAR 1101(00FE,0100)]s", Line(RulePatches.ItemMessageFile));
            Assert.Equal(RulePatches.SuperCandyDescription, Line(RulePatches.ItemFlavorFile));

            var all = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "6", "1")));
            Assert.Equal(before + 1, all.FileCount);
            var original = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(icons));
            Assert.All(Enumerable.Range(0, before), i => Assert.Equal(original[i], all[i]));

            // El icono del juego se lee igual que lo pinta la app: rojo donde era azul, con el aura alrededor.
            var drawn = Sprites.BflimTexture.Decode(all[before]);
            var candy = Sprites.BflimTexture.Decode(original[RulePatches.RareCandyItem - 1]);
            var painted = Sprites.SuperCandyIcon.Paint(candy.Pixels, candy.Width, candy.Height);
            Assert.Equal((32, 32), (drawn.Width, drawn.Height));
            Assert.All(Enumerable.Range(0, drawn.Pixels.Length), i => Assert.Equal(painted[i] >> 3, drawn.Pixels[i] >> 3));
            Assert.True(drawn.Pixels.Where((_, i) => i % 4 == 3 && drawn.Pixels[i] != 0).Count()
                        > candy.Pixels.Where((_, i) => i % 4 == 3 && candy.Pixels[i] != 0).Count());

            var records = RulePatches.SuperCandyRecords(File.ReadAllBytes(code), icon!.Value)!;
            Assert.Equal((uint)icon, BinaryPrimitives.ReadUInt32LittleEndian(records.Single(r => r.Offset == RulePatches.ItemIconTable + 4 * SuperCandy.ItemId).Bytes));
            Assert.Equal(11, records.Count);
        }
        finally
        {
            Directory.Delete(romfs, true);
        }
    }

    [Fact]
    public void The_mods_code_gets_its_records_on_the_rare_candy_check_the_hp_store_and_the_empty_cave()
    {
        if (Expansion("exefs", "code.bin") is not { } path) return;

        var records = RulePatches.CodeRecords(File.ReadAllBytes(path));

        Assert.NotNull(records);
        Assert.Equal([RulePatches.FallenSite, RulePatches.DupeSite, RulePatches.CandySite, RulePatches.DupeCave, RulePatches.CodeCave, RulePatches.FallenCave, RulePatches.DupeTestCave],
            records!.Select(r => r.Offset).Order());
    }

    [Fact]
    public void The_repel_steps_stay_put_when_the_infinite_repel_set_them()
    {
        Assert.Equal("add r0, r0, #0xa00; ldrh r2, [r0, #0x3e]; cmp r2, #0x72; ldrhne r1, [r0, #0x58]; cmpne r1, #0; "
                     + "moveq r0, #0; bxeq lr; subs r1, r1, #1; strh r1, [r0, #0x58]; moveq r0, #1; movne r0, #0; bx lr",
            Disassemble(RulePatches.RepelCount(), RulePatches.RepelCountSite + 0x100000));
        Assert.Equal(48, RulePatches.RepelCount().Length);
    }

    [Fact]
    public void The_bag_sends_the_infinite_repel_to_its_routine_which_turns_it_on_and_off()
    {
        Assert.Equal("cmp r5, #0x72; beq #0x16c18; cmp r5, #0x73; beq #0x16c28; cmp r3, r5; bx lr; mov r0, r4; mov r1, r5; pop {r3, r4, r5, r6, r7, lr}; b #0x16c38; "
                     + "mov r0, r4; mov r1, r5; pop {r3, r4, r5, r6, r7, lr}; b #0x16ce8",
            Disassemble(RulePatches.BagLookup(), RulePatches.BagCave));
        Assert.Equal("push {r4, r5, r6, lr}; mov r4, r0; mov r5, r1; ldr r0, [r4, #0x4c]; add r3, r0, #0xa00; "
                     + "ldrh r2, [r3, #0x58]; ldrh r3, [r3, #0x3e]; subs r6, r3, r5; movne r6, #1; cmp r2, #0; moveq r6, #1; "
                     + "mov r2, r6; mov r1, r5; bl #0x738; ldr r0, [r4, #0x70]; cmp r6, #0; moveq r1, #0x3c; movne r1, #0x3b; "
                     + "bl #0x16ca0; ldr r0, [r4, #0x70]; mov r1, #0; bl #0xa5e4; mov r0, #2; str r0, [r4, #4]; mov r0, #0; "
                     + "pop {r4, r5, r6, pc}",
            Disassemble(RulePatches.BagHandlerCode(), RulePatches.BagCave + 0x38));
        Assert.Equal("push {r4, r5, sb, sl, fp, lr}; mov r4, r0; mov r5, r1; add r0, r0, #4; bl #0x3a8; mov sl, r0; mov r1, #1; "
                     + "bl #0x460; mov sb, r0; mov r1, r0; mov r2, r5; mov r0, sl; bl #0x590; mov r1, sb; mov r0, r4; mov r2, #1; "
                     + "pop {r4, r5, sb, sl, fp, lr}; b #0xae20",
            Disassemble(RulePatches.BagShowCode(), RulePatches.BagCave + 0xA0));
        Assert.Equal(0x38, RulePatches.BagLookup().Length);
        Assert.Equal(0x68, RulePatches.BagHandlerCode().Length);
        Assert.Equal((RulePatches.RepelOnLine, RulePatches.RepelOffLine), (0x3B, 0x3C));
    }

    [Fact]
    public void The_mods_bag_takes_the_hook_and_the_cave_and_nothing_else()
    {
        if (Expansion("romfs", "Bag.cro") is not { } path) return;

        var original = File.ReadAllBytes(path);
        var bytes = (byte[])original.Clone();

        Assert.Equal(RulePatches.BattleState.Patched, RulePatches.PatchBag(bytes));
        var once = (byte[])bytes.Clone();
        Assert.Equal(RulePatches.BattleState.AlreadyPatched, RulePatches.PatchBag(bytes));
        Assert.Equal(once, bytes);

        // Una cueva de una versión anterior se corrige con el gancho ya puesto.
        bytes[RulePatches.BagCave + 0x30] ^= 0xFF;
        Assert.Equal(RulePatches.BattleState.Patched, RulePatches.PatchBag(bytes));
        Assert.Equal(once, bytes);
        Assert.Equal("bl #0x16c00", Disassemble(bytes[RulePatches.BagLookupSite..(RulePatches.BagLookupSite + 4)], RulePatches.BagLookupSite));

        // El código del segmento acaba en 0x16DFC: la cueva cabe dentro.
        var changed = Enumerable.Range(0, bytes.Length).Where(i => bytes[i] != original[i]).ToList();
        Assert.All(changed, i => Assert.True(
            i is >= RulePatches.BagLookupSite and < RulePatches.BagLookupSite + 4 || i is >= RulePatches.BagCave and < 0x16DFC));

        Assert.Equal(RulePatches.BattleState.Unknown, RulePatches.PatchBag(new byte[original.Length]));
    }

    [Fact]
    public void The_infinite_repel_goes_into_the_mods_files_once_next_to_the_super_candy()
    {
        if (Expansion("romfs", "a", "0", "1", "9") is not { } data || Expansion("romfs", "a", "0", "3", "6") is not { } text
            || Expansion("romfs", "a", "0", "6", "1") is not { } icons || Expansion("exefs", "code.bin") is not { } code) return;

        var romfs = Path.Combine(Path.GetTempPath(), "permalocke-repel-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var (from, parts) in new[] { (data, "1/9"), (text, "3/6"), (icons, "6/1") })
            {
                var to = Path.Combine([romfs, "a", "0", .. parts.Split('/')]);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to);
            }

            var before = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(icons)).FileCount;
            var super = RulePatches.InstallSuperCandy(romfs, out var problem);
            Assert.Null(problem);
            var icon = RulePatches.InstallInfiniteRepel(romfs, out problem);
            Assert.Null(problem);
            Assert.Equal((before, before + 1), (super, icon));
            Assert.Equal(super, RulePatches.InstallSuperCandy(romfs, out problem));
            Assert.Equal(icon, RulePatches.InstallInfiniteRepel(romfs, out problem));
            Assert.Null(problem);

            var items = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "1", "9")));
            Assert.Equal(items[RulePatches.ExpShareItem], items[InfiniteRepel.ItemId]);

            var config = new pk3DS.Core.GameConfig(pk3DS.Core.GameVersion.UM);
            var garc = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "3", "6")));
            string Line(int file, int line = InfiniteRepel.ItemId) => pk3DS.Core.TextFile.GetStrings(config, garc[file])[line];
            Assert.Equal(InfiniteRepel.Name, Line(RulePatches.ItemNamesFile));
            Assert.Equal("Repelentes Infinitos", Line(RulePatches.ItemPluralFile));
            Assert.Equal(InfiniteRepel.Name + "[VAR 1101(00FE,0000)]", Line(RulePatches.ItemMessageFile));
            Assert.Equal(RulePatches.InfiniteRepelDescription, Line(RulePatches.ItemFlavorFile));
            Assert.Equal(RulePatches.RepelOnText, Line(RulePatches.BagTextFile, RulePatches.RepelOnLine));
            Assert.Equal(RulePatches.RepelOffText, Line(RulePatches.BagTextFile, RulePatches.RepelOffLine));
            Assert.Equal(@"El Repelente sigue haciendo efecto.\nNo puedes usar otro todavía.", Line(RulePatches.BagTextFile, 78));

            var all = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "6", "1")));
            Assert.Equal(before + 2, all.FileCount);
            var repel = Sprites.BflimTexture.Decode(new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(icons))[RulePatches.RepelItem - 1]);
            var painted = Sprites.InfiniteRepelIcon.Paint(repel.Pixels, repel.Width, repel.Height);
            var drawn = Sprites.BflimTexture.Decode(all[icon!.Value]);
            Assert.All(Enumerable.Range(0, drawn.Pixels.Length), i => Assert.Equal(painted[i] >> 3, drawn.Pixels[i] >> 3));

            var records = RulePatches.InfiniteRepelRecords(File.ReadAllBytes(code), icon.Value)!;
            Assert.Equal([RulePatches.RepelCountSite, RulePatches.ItemIconTable + 4 * InfiniteRepel.ItemId], records.Select(r => r.Offset));
            Assert.Equal((uint)icon, BinaryPrimitives.ReadUInt32LittleEndian(records[1].Bytes));
        }
        finally
        {
            Directory.Delete(romfs, true);
        }
    }

    [Fact]
    public void The_incubator_goes_into_the_mods_files_after_the_repel_and_its_code_reads_as_written()
    {
        if (Expansion("romfs", "a", "0", "1", "9") is not { } data || Expansion("romfs", "a", "0", "3", "6") is not { } text
            || Expansion("romfs", "a", "0", "6", "1") is not { } icons || Expansion("exefs", "code.bin") is not { } code) return;

        var romfs = Path.Combine(Path.GetTempPath(), "permalocke-egg-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (var (from, parts) in new[] { (data, "1/9"), (text, "3/6"), (icons, "6/1") })
            {
                var to = Path.Combine([romfs, "a", "0", .. parts.Split('/')]);
                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Copy(from, to);
            }

            var before = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(icons)).FileCount;
            Assert.Equal(before, RulePatches.InstallSuperCandy(romfs, out var problem));
            Assert.Equal(before + 1, RulePatches.InstallInfiniteRepel(romfs, out problem));
            var icon = RulePatches.InstallEggTurbo(romfs, out problem);
            Assert.Null(problem);
            Assert.Equal(before + 2, icon);
            Assert.Equal(icon, RulePatches.InstallEggTurbo(romfs, out problem));
            Assert.Null(problem);

            var items = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "1", "9")));
            Assert.Equal(items[RulePatches.ExpShareItem], items[EggTurbo.ItemId]);

            var config = new pk3DS.Core.GameConfig(pk3DS.Core.GameVersion.UM);
            var garc = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "3", "6")));
            string Line(int file, int line = EggTurbo.ItemId) => pk3DS.Core.TextFile.GetStrings(config, garc[file])[line];
            Assert.Equal(EggTurbo.Name, Line(RulePatches.ItemNamesFile));
            Assert.Equal("Incubadoras Turbo", Line(RulePatches.ItemPluralFile));
            Assert.Equal(RulePatches.EggTurboDescription, Line(RulePatches.ItemFlavorFile));
            Assert.Equal(RulePatches.EggTurboOnText, Line(RulePatches.BagTextFile, RulePatches.EggTurboOnLine));
            Assert.Equal(RulePatches.EggTurboOffText, Line(RulePatches.BagTextFile, RulePatches.EggTurboOffLine));
            Assert.Equal(RulePatches.RepelOnText, Line(RulePatches.BagTextFile, RulePatches.RepelOnLine));

            var all = new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(Path.Combine(romfs, "a", "0", "6", "1")));
            var template = Sprites.BflimTexture.Decode(new pk3DS.Core.CTR.GARC.LazyGARC(File.ReadAllBytes(icons))[RulePatches.RepelItem - 1]);
            var painted = Sprites.EggTurboIcon.Paint(template.Pixels, template.Width, template.Height);
            var drawn = Sprites.BflimTexture.Decode(all[icon!.Value]);
            Assert.All(Enumerable.Range(0, drawn.Pixels.Length), i => Assert.Equal(painted[i] >> 3, drawn.Pixels[i] >> 3));

            var bytes = File.ReadAllBytes(code);
            var records = RulePatches.EggTurboRecords(bytes, icon.Value)!;
            Assert.Equal([RulePatches.HatchSite, RulePatches.FamiliarityAmountSite, RulePatches.ItemIconTable + 4 * EggTurbo.ItemId], records.Select(r => r.Offset));
            Assert.Equal((uint)icon, BinaryPrimitives.ReadUInt32LittleEndian(records[2].Bytes));

            // El código nuevo, leído con el desensamblador: con el byte puesto 0x8001; si no, n o 1,5 n; y el cuerpo viejo no queda.
            var hatch = records[0].Bytes;
            Assert.Equal(26 * 4, hatch.Length);
            Assert.Equal(
                "ldr r2, [pc, #0x5c]; ldrb r2, [r2]; cmp r2, #0; movne r0, #0x8000; addne r0, r0, #1; bxne lr; ldrb r2, [r0, #0x28]; "
                + "mov r0, r1; cmp r2, #1; addeq r0, r1, r1, lsr #1; bx lr; mov r5, r1; ldr ip, [pc, #0x2c]; ldrb ip, [ip]; cmp ip, #0; movne r5, #0xff; bx lr",
                Disassemble(hatch[..(17 * 4)], RulePatches.HatchSite));
            Assert.Equal(RuleBlock.EggTurbo, BinaryPrimitives.ReadUInt32LittleEndian(hatch.AsSpan(25 * 4)));
            Assert.Equal($"bl #0x{RulePatches.HatchSite + 44:x}", Disassemble(records[1].Bytes, RulePatches.FamiliarityAmountSite));

            // El cuerpo del juego no cambia más que en esas 26 palabras y la llamada, y con otro code.bin no se toca nada.
            Assert.Null(RulePatches.EggTurboRecords(new byte[bytes.Length], icon.Value));
        }
        finally
        {
            Directory.Delete(romfs, true);
        }
    }

    [Fact]
    public void The_bag_routes_both_key_items_and_the_incubator_flips_the_blocks_byte()
    {
        var lookup = RulePatches.BagLookup();
        Assert.Equal(14 * 4, lookup.Length);
        Assert.Equal(
            "cmp r5, #0x72; beq #0x16c18; cmp r5, #0x73; beq #0x16c28; cmp r3, r5; bx lr; mov r0, r4; mov r1, r5; pop {r3, r4, r5, r6, r7, lr}; "
            + $"b #0x{0x16C00 + 0x38:x}; mov r0, r4; mov r1, r5; pop {{r3, r4, r5, r6, r7, lr}}; b #0x{0x16C00 + 0x38 + 0x68 + 0x48:x}",
            Disassemble(lookup, 0x16C00));

        var egg = RulePatches.BagEggCode();
        Assert.Equal(19 * 4, egg.Length);
        Assert.Equal(RuleBlock.EggTurbo, BinaryPrimitives.ReadUInt32LittleEndian(egg.AsSpan(18 * 4)));
        var at = 0x16C00 + 0x38 + 0x68 + 0x48;
        Assert.Equal(
            "push {r4, r5, r6, lr}; mov r4, r0; ldr r2, [pc, #0x38]; ldrb r3, [r2]; eor r6, r3, #1; strb r6, [r2]; ldr r0, [r4, #0x70]; cmp r6, #0; "
            + "moveq r1, #0x5c; movne r1, #0x5b",
            Disassemble(egg[..(10 * 4)], at));
    }
}
