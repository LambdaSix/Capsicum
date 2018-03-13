namespace Capsicum.Serialization.Binary {
    public static class Constants {
        public static int FileVersion = 1;

        public static byte EntityStart = 0xFA;
        public static byte EntityEnd = 0xFB;

        public static byte ComponentStart = 0xF1;
        public static byte ComponentEnd = 0xF2;
    }
}