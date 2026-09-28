namespace RotkAlive.Tests
{
    // Lines copied from C:\Games\ROTK\Logs on 2026-09-27 unless marked synthetic.
    // Embedded here because the live files are replaced on every client launch.
    static class Fixtures
    {
        // Brief session, client run 1790529693.
        public const string BriefStargetPacket = "2026-09-27\t19:24:02\tDESKTOP-0SJTF3G\t1790529693\t10954\t2\t450366918\tHandleMatchStargetPacket";
        public const string BriefStart = "2026-09-27\t19:24:02\tDESKTOP-0SJTF3G\t1790529693\t10955\t2\t450366918\tEVENT_START_MATCH";
        public const string BriefKill = "2026-09-27\t19:24:21\t[HOST]\t1790529693\t11721\t4\t450385324\ttheCITYisRED (3497557662702907908) [rank:5.3] [ping:3] KILLED Cyb (5619943147552144769) [rank:4.3] [ping:3]";
        public const string BriefHeadshot = "2026-09-27\t19:24:21\t[HOST]\t1790529693\t11724\t4\t450385724\tScreedy (10206652050108690840) [rank:5.4] [ping:3] KILLED Karspin (13263735598424155381) [rank:4.4] [ping:3] HEADSHOT";
        public const string BriefAssist = "2026-09-27\t19:24:26\t[HOST]\t1790529693\t11740\t4\t450390373\tLekid (6758572958123711573) [rank:5.3] [ping:3] (ASSIST Horizon_ (14461580563613653473) [rank:5.3] [ping:3]) KILLED Hostyle (17945733976376050602) [rank:5.1] [ping:3]";
        public const string BriefDeath = "2026-09-27\t19:29:18\t[HOST]\t1790529693\t13794\t4\t450682643\tDEATH KingDady (12808836654268928371) [rank:5.3] [ping:3]";
        public const string BriefHostChanged = "2026-09-27\t19:58:40\tDESKTOP-0SJTF3G\t1790529693\t23143\t4\t452444332\tkapiszi (7258563311314439046) [rank:4.2] [ping:3] KILLED rmbx (16794321960974638935) [rank:5.3] [ping:3]";

        // Live session, client run 1790534889.
        public const string Start1 = "2026-09-27\t20:51:55\tDESKTOP-0SJTF3G\t1790534889\t10771\t2\t455638964\tEVENT_START_MATCH";
        public const string Start2 = "2026-09-27\t20:54:59\tDESKTOP-0SJTF3G\t1790534889\t12675\t2\t455823048\tEVENT_START_MATCH";
        // Synthetic seq/tick and flag value, real message shapes.
        public const string Stargetpacket2 = "2026-09-27\t20:54:59\tDESKTOP-0SJTF3G\t1790534889\t12674\t2\t455823048\tHandleMatchStargetPacket";
        public const string SummaryNoise = "2026-09-27\t20:55:14\tDESKTOP-0SJTF3G\t1790534889\t13340\t2\t455838400\tHandleMatchPlayerSummaryPacket. InputFlags: 0x00000001";

        public const string M1Kill1 = "2026-09-27\t20:52:12\tDESKTOP-0SJTF3G\t1790534889\t11560\t4\t455656478\tN4KL (580198822161572562) [rank:4.1] [ping:3] KILLED Tottinho (13856340092514558465) [rank:5.1] [ping:3]";
        public const string M1Kill2 = "2026-09-27\t20:52:12\tDESKTOP-0SJTF3G\t1790534889\t11562\t4\t455656564\tleodakappa (4775357920797685090) [rank:4.3] [ping:3] KILLED TemPzLord (4491479118157081217) [rank:5.5] [ping:3] HEADSHOT";
        public const string M1Kill3 = "2026-09-27\t20:52:13\tDESKTOP-0SJTF3G\t1790534889\t11568\t4\t455657572\tlIlIlIlIlIlIlIlIlIlI (2687193247964705511) [rank:5.4] [ping:3] KILLED Dr_Quinnzel (13549629962093945521) [rank:5.2] [ping:3] HEADSHOT";

        public const string M2DaqzzKillsSurvivor = "2026-09-27\t20:55:21\tDESKTOP-0SJTF3G\t1790534889\t13430\t4\t455845900\tdaqzz (14622665195573461951) [rank:1.2] [ping:3] KILLED Survivor63153485 (10344157588046667421) [rank:0.0] [ping:3]";
        public const string M2PosthumousAssist = "2026-09-27\t20:55:32\tDESKTOP-0SJTF3G\t1790534889\t13521\t4\t455856982\tBARA NO STOP (422556523889196780) [rank:5.2] [ping:3] (ASSIST Survivor63153485 (10344157588046667421) [rank:0.2] [ping:3]) KILLED daqzz (14622665195573461951) [rank:1.2] [ping:3]";
        public const string M2SousFiltre = "2026-09-27\t20:55:39\tDESKTOP-0SJTF3G\t1790534889\t13551\t4\t455863480\tSous 3x Filtré (13001428296672168345) [rank:4.4] [ping:3] KILLED possaki154 (6522300550184131816) [rank:7.3] [ping:3]";
        public const string M2ZeroRank = "2026-09-27\t20:56:00\tDESKTOP-0SJTF3G\t1790534889\t13717\t4\t455884426\tSurvivor15641363 (3928683307583663962) [rank:0.0] [ping:3] KILLED handsome (15092664460531249093) [rank:4.2] [ping:3]";
        public const string M2TytKillsKayzah = "2026-09-27\t20:56:55\tDESKTOP-0SJTF3G\t1790534889\t14207\t4\t455938929\ttYt_DSN2tap (7652327888861413276) [rank:4.4] [ping:3] KILLED kayzahMACHINE (8302646291024083693) [rank:5.5] [ping:3]";
        public const string M2KayzahPosthumousKill = "2026-09-27\t20:56:56\tDESKTOP-0SJTF3G\t1790534889\t14222\t4\t455940061\tkayzahMACHINE (8302646291024083693) [rank:5.5] [ping:3] KILLED tYt_DSN2tap (7652327888861413276) [rank:4.4] [ping:3]";
        public const string M2TottinhoDies = "2026-09-27\t20:57:33\tDESKTOP-0SJTF3G\t1790534889\t14492\t4\t455977112\tLapurge76 (7409795704159837713) [rank:3.3] [ping:3] KILLED Tottinho (3650647432991908622) [rank:5.1] [ping:3] HEADSHOT";

        public const string M2DoggeinfKill1 = "2026-09-27\t20:55:17\tDESKTOP-0SJTF3G\t1790534889\t13396\t4\t455841365\tdoggeinf (10484055460605013394) [rank:4.2] [ping:3] KILLED marcola-DsnipA. (14712544754286835018) [rank:3.3] [ping:3]";
        public const string M2DoggeinfKill2 = "2026-09-27\t20:55:31\tDESKTOP-0SJTF3G\t1790534889\t13511\t4\t455855904\tdoggeinf (10484055460605013394) [rank:4.2] [ping:3] KILLED TomboTom (12882885702838419897) [rank:5.5] [ping:3]";

        // Synthetic: first rank sticks (killer at 5.3, later an assist at 5.2).
        public const string SynthSanchezKill = "2026-09-27\t21:00:10\tDESKTOP-0SJTF3G\t1790534889\t20001\t4\t456100000\tSanchezZzTV (111111111111111111) [rank:5.3] [ping:3] KILLED Alpha (222222222222222222) [rank:3.0] [ping:3]";
        public const string SynthSanchezAssist = "2026-09-27\t21:00:20\tDESKTOP-0SJTF3G\t1790534889\t20002\t4\t456110000\tBravo (333333333333333333) [rank:4.0] [ping:3] (ASSIST SanchezZzTV (111111111111111111) [rank:5.2] [ping:3]) KILLED Charlie (444444444444444444) [rank:2.0] [ping:3]";
        public const string SynthStart3 = "2026-09-27\t21:00:00\tDESKTOP-0SJTF3G\t1790534889\t20000\t2\t456090000\tEVENT_START_MATCH";

        // Synthetic unknown shapes.
        public const string SynthTwoAssists = "2026-09-27\t21:00:30\tDESKTOP-0SJTF3G\t1790534889\t20003\t4\t456120000\tDelta (555555555555555555) [rank:6.1] [ping:3] (ASSIST Echo (666666666666666666) [rank:4.0] [ping:3]) (ASSIST Fox (777777777777777777) [rank:4.1] [ping:3]) KILLED Golf (888888888888888888) [rank:5.0] [ping:3] HEADSHOT";
        public const string SynthDeathSuffix = "2026-09-27\t21:00:31\tDESKTOP-0SJTF3G\t1790534889\t20004\t4\t456121000\tDEATH Hotel (999999999999999999) [rank:2.0] [ping:3] (gas)";

        // Live session 2026-09-28, client run 1790595332: trailing friend flags.
        public const string FriendStart = "2026-09-28\t13:50:15\tDESKTOP-0SJTF3G\t1790595332\t11225\t2\t516742000\tEVENT_START_MATCH";
        public const string FriendHeadshot = "2026-09-28\t13:50:32\tDESKTOP-0SJTF3G\t1790595332\t11912\t4\t516759732\tTottinho (13856340092514558465) [rank:5.3] [ping:3] KILLED LA VACHE QUI RIT (594189586983620436) [rank:7.5] [ping:3] HEADSHOT KILLERFRIEND";
        public const string FriendKill = "2026-09-28\t13:50:35\tDESKTOP-0SJTF3G\t1790595332\t11966\t4\t516763185\tLollo_458 (17778889448668183903) [rank:5.3] [ping:3] KILLED amzzor (810587526584891985) [rank:6.1] [ping:3] KILLERFRIEND";
        public const string FriendAssist = "2026-09-28\t13:56:27\tDESKTOP-0SJTF3G\t1790595332\t14372\t4\t517114674\txiaobo (1342136868026594925) [rank:4.3] [ping:3] (ASSIST Lollo_458 (17778889448668183903) [rank:5.3] [ping:3]) KILLED MrAtchoum (13729704247481210739) [rank:3.2] [ping:3] ASSISTFRIEND";
        public const string SynthGarbage = "2026-09-27\t21:00:32\tDESKTOP-0SJTF3G\t1790534889\t20005\t4\t456122000\tsomething new the client started printing";
        public const string SynthShort = "2026-09-27\t21:00:33\tDESKTOP-0SJTF3G\tbroken";
    }
}
