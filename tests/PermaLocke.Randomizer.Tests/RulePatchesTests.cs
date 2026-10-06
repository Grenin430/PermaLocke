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
    public void The_mods_code_gets_its_records_on_the_rare_candy_check_the_hp_store_and_the_empty_cave()
    {
        if (Expansion("exefs", "code.bin") is not { } path) return;

        var records = RulePatches.CodeRecords(File.ReadAllBytes(path));

        Assert.NotNull(records);
        Assert.Equal([RulePatches.FallenSite, RulePatches.DupeSite, RulePatches.CandySite, RulePatches.DupeCave, RulePatches.CodeCave, RulePatches.FallenCave, RulePatches.DupeTestCave],
            records!.Select(r => r.Offset).Order());
    }
}
