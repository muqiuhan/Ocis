// ignore_for_file: camel_case_types, constant_identifier_names, non_constant_identifier_names, unnecessary_this
import './fable_modules/fable_library/Encoding.dart' as encoding;

int getUint32(int b0, int b1, int b2, int b3) => (((((b0 | ((b1 << 8) >>> 0)) >>> 0) | ((b2 << 16) >>> 0)) >>> 0) | ((b3 << 24) >>> 0)) >>> 0;

int getInt32(int b0, int b1, int b2, int b3) => ((b0 | (b1 << 8)) | (b2 << 16)) | (b3 << 24);

int readUInt32LittleEndian(List<int> buffer, int offset) => getUint32(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);

int readInt32LittleEndian(List<int> buffer, int offset) => getInt32(buffer[offset], buffer[offset + 1], buffer[offset + 2], buffer[offset + 3]);

int readByte(List<int> buffer, int offset) => buffer[offset];

void writeUInt32LittleEndian(int value, List<int> buffer, int offset) {
    buffer[offset] = value;
    buffer[offset + 1] = value >>> 8;
    buffer[offset + 2] = value >>> 16;
    buffer[offset + 3] = value >>> 24;
}

void writeInt32LittleEndian(int value, List<int> buffer, int offset) {
    final u = value;
    buffer[offset] = u;
    buffer[offset + 1] = u >>> 8;
    buffer[offset + 2] = u >>> 16;
    buffer[offset + 3] = u >>> 24;
}

void writeByte(int value, List<int> buffer, int offset) {
    buffer[offset] = value;
}

List<int> stringToBytes(String str) => encoding.get_UTF8().getBytes(str);

String bytesToString(List<int> bytes) => encoding.get_UTF8().getString(bytes);

