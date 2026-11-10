import com.dabomstew.pkrandom.pokemon.Move;
import com.dabomstew.pkrandom.pokemon.MoveCategory;
import com.dabomstew.pkrandom.pokemon.MoveLearnt;
import com.dabomstew.pkrandom.pokemon.Trainer;
import com.dabomstew.pkrandom.pokemon.TrainerPokemon;
import com.dabomstew.pkrandom.romhandlers.Gen4RomHandler;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Random;
import java.util.Set;

/**
 * PermaLocke's own step after the Universal Randomizer, for what it has no option for (HeartGold, 2026-11-10):
 * - no Master Ball lying on the ground: each one becomes an Ultra Ball;
 * - nobody can have a one-hit KO move (Guillotine, Horn Drill, Fissure, Sheer Cold), as in Ultra Luna: in a level-up list it becomes a status
 *   move the Pokemon did not have (power 0 too, so the order by power does not move), in egg moves and trainers' own moves any other move.
 * Usage: java -cp PokeRandoZX.jar;PermaLocke-upr.jar PermaLockeTweaks <rom.nds>. Writes the ROM in place only when something changed.
 */
public class PermaLockeTweaks {
    private static final int MASTER_BALL = 1;
    private static final int ULTRA_BALL = 2;
    private static final Set<Integer> BANNED = Set.of(12, 32, 90, 329);
    private static final int STRUGGLE = 165;

    public static void main(String[] args) {
        Gen4RomHandler rom = new Gen4RomHandler();
        if (!rom.loadRom(args[0])) { System.err.println("No se ha podido leer la ROM"); System.exit(2); }
        Random random = new Random(20261110L);

        List<Integer> statusMoves = new ArrayList<>();
        List<Integer> anyMoves = new ArrayList<>();
        for (Move move : rom.getMoves()) {
            if (move == null || move.number == 0 || move.number == STRUGGLE || BANNED.contains(move.number)) continue;
            anyMoves.add(move.number);
            if (move.category == MoveCategory.STATUS) statusMoves.add(move.number);
        }

        List<Integer> items = new ArrayList<>(rom.getRegularFieldItems());
        int balls = 0;
        for (int i = 0; i < items.size(); i++) {
            if (items.get(i) == MASTER_BALL) { items.set(i, ULTRA_BALL); balls++; }
        }
        if (balls > 0) rom.setRegularFieldItems(items);

        Map<Integer, List<MoveLearnt>> learnt = rom.getMovesLearnt();
        int levelUp = 0;
        for (List<MoveLearnt> list : learnt.values()) {
            Set<Integer> known = new HashSet<>();
            for (MoveLearnt m : list) known.add(m.move);
            for (MoveLearnt m : list) {
                if (BANNED.contains(m.move)) { m.move = pick(statusMoves, known, random); known.add(m.move); levelUp++; }
            }
        }
        if (levelUp > 0) rom.setMovesLearnt(learnt);

        Map<Integer, List<Integer>> eggs = rom.getEggMoves();
        int egg = 0;
        for (List<Integer> list : eggs.values()) {
            Set<Integer> known = new HashSet<>(list);
            for (int i = 0; i < list.size(); i++) {
                if (BANNED.contains(list.get(i))) { int m = pick(anyMoves, known, random); list.set(i, m); known.add(m); egg++; }
            }
        }
        if (egg > 0) rom.setEggMoves(eggs);

        List<Trainer> trainers = rom.getTrainers();
        int trainer = 0;
        for (Trainer t : trainers) {
            for (TrainerPokemon p : t.pokemon) {
                if (p.moves == null) continue;
                Set<Integer> known = new HashSet<>();
                for (int m : p.moves) known.add(m);
                for (int i = 0; i < p.moves.length; i++) {
                    if (BANNED.contains(p.moves[i])) { p.moves[i] = pick(anyMoves, known, random); known.add(p.moves[i]); trainer++; }
                }
            }
        }
        if (trainer > 0) rom.setTrainers(trainers);

        if (balls + levelUp + egg + trainer > 0 && !rom.saveRom(args[0], 0L, false)) {
            System.err.println("No se ha podido escribir la ROM"); System.exit(3);
        }
        System.out.println("Master Balls cambiadas: " + balls + ". Fulminantes quitados: " + levelUp + " por nivel, " + egg + " huevo, " + trainer + " entrenadores.");
    }

    private static int pick(List<Integer> from, Set<Integer> known, Random random) {
        for (int tries = 0; tries < 1000; tries++) {
            int m = from.get(random.nextInt(from.size()));
            if (!known.contains(m)) return m;
        }
        return from.get(0);
    }
}
