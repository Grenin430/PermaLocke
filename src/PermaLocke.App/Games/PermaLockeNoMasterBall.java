import com.dabomstew.pkrandom.romhandlers.Gen4RomHandler;
import java.util.ArrayList;
import java.util.List;

/**
 * PermaLocke, after the Universal Randomizer: no Master Ball lying on the ground of HeartGold (the Randomizer has no option to ban it).
 * Each one becomes an Ultra Ball. Usage: java -cp PokeRandoZX.jar;this.jar PermaLockeNoMasterBall <rom.nds>. Prints how many it changed.
 */
public class PermaLockeNoMasterBall {
    private static final int MASTER_BALL = 1;
    private static final int ULTRA_BALL = 2;

    public static void main(String[] args) {
        Gen4RomHandler rom = new Gen4RomHandler();
        if (!rom.loadRom(args[0])) { System.err.println("No se ha podido leer la ROM"); System.exit(2); }
        List<Integer> items = new ArrayList<>(rom.getRegularFieldItems());
        int changed = 0;
        for (int i = 0; i < items.size(); i++) {
            if (items.get(i) == MASTER_BALL) { items.set(i, ULTRA_BALL); changed++; }
        }
        if (changed > 0) {
            rom.setRegularFieldItems(items);
            if (!rom.saveRom(args[0], 0L, false)) { System.err.println("No se ha podido escribir la ROM"); System.exit(3); }
        }
        System.out.println("Master Balls cambiadas: " + changed);
    }
}
