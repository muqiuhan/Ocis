// ignore_for_file: camel_case_types, constant_identifier_names, non_constant_identifier_names, unnecessary_this
import './Ocis.Server/ProtocolSpec.dart' as protocol_spec;
import './fable_modules/fable_library/Array.dart' as array;
import './fable_modules/fable_library/Encoding.dart' as encoding;
import './fable_modules/fable_library/Option.dart' as option_4;
import './fable_modules/fable_library/String.dart' as string;
import './fable_modules/fable_library/Types.dart' as types;
import './fable_modules/fable_library/Util.dart' as util;
import './Binary.dart' as binary;

types.Some<protocol_spec.RequestPacket>? TryParseRequestHeader(List<int> buffer) {
    if (buffer.length < 18) {
        return null;
    } else {
        try {
            final magicNumber = binary.readUInt32LittleEndian(buffer, 0);
            final version = binary.readByte(buffer, 4);
            if ((magicNumber == 1397310287) && (version == 1)) {
                return types.Some(protocol_spec.RequestPacket(magicNumber, version, binary.readByte(buffer, 5), binary.readInt32LittleEndian(buffer, 6), binary.readInt32LittleEndian(buffer, 10), binary.readInt32LittleEndian(buffer, 14), <int>[], null));
            } else {
                return null;
            }
        } catch (matchValue) {
            return null;
        }
    }
}

types.Some<protocol_spec.RequestPacket>? TryParseRequestPacket(List<int> buffer) {
    final types.Some<protocol_spec.RequestPacket>? matchValue = TryParseRequestHeader(buffer);
    if (matchValue == null) {
        return null;
    } else {
        final header = types.value(matchValue);
        if (buffer.length >= header.TotalPacketLength) {
            try {
                return types.Some(protocol_spec.RequestPacket(header.MagicNumber, header.Version, header.CommandType, header.TotalPacketLength, header.KeyLength, header.ValueLength, array.getSubArray<int>(buffer, 18, header.KeyLength), (header.ValueLength > 0) ? types.Some(array.getSubArray<int>(buffer, 18 + header.KeyLength, header.ValueLength)) : null));
            } catch (matchValue_1) {
                return null;
            }
        } else {
            return null;
        }
    }
}

List<int> SerializeRequest(protocol_spec.RequestPacket packet) {
    final keyLen = packet.Key.length;
    final totalLen = (18 + keyLen) + option_4.defaultValue(0, option_4.map<List<int>, int>((List<int> v) => v.length, packet.Value));
    final buffer = List.filled(totalLen, 0);
    var offset = 0;
    binary.writeUInt32LittleEndian(packet.MagicNumber, buffer, offset);
    offset = offset + 4;
    binary.writeByte(packet.Version, buffer, offset);
    offset = offset + 1;
    binary.writeByte(packet.CommandType, buffer, offset);
    offset = offset + 1;
    binary.writeInt32LittleEndian(packet.TotalPacketLength, buffer, offset);
    offset = offset + 4;
    binary.writeInt32LittleEndian(packet.KeyLength, buffer, offset);
    offset = offset + 4;
    binary.writeInt32LittleEndian(packet.ValueLength, buffer, offset);
    offset = offset + 4;
    if (keyLen > 0) {
        array.copyTo<int>(packet.Key, 0, buffer, offset, keyLen);
        offset = offset + keyLen;
    }
    final types.Some<List<int>>? matchValue = packet.Value;
    if (matchValue == null) {
    } else {
        final value_1 = types.value(matchValue);
        array.copyTo<int>(value_1, 0, buffer, offset, value_1.length);
    }
    return buffer;
}

protocol_spec.ResponsePacket CreateSuccessResponse(types.Some<List<int>>? value) {
    late final types.Tuple2<int, int> patternInput;
    if (value == null) {
        patternInput = const types.Tuple2(0, 18);
    } else {
        final v = types.value(value);
        patternInput = types.Tuple2(v.length, 18 + v.length);
    }
    return protocol_spec.ResponsePacket(1397310287, 1, 0, patternInput.item2, patternInput.item1, 0, value, null);
}

protocol_spec.ResponsePacket CreateNotFoundResponse() => const protocol_spec.ResponsePacket(1397310287, 1, 1, 18, 0, 0, null, null);

protocol_spec.ResponsePacket CreateErrorResponse(String errorMessage) {
    final msgBytes = encoding.get_UTF8().getBytes(errorMessage);
    return protocol_spec.ResponsePacket(1397310287, 1, 2, 18 + msgBytes.length, 0, msgBytes.length, null, types.Some(errorMessage));
}

bool IsValidPacketSize(int totalLength) {
    if (totalLength >= 18) {
        return totalLength <= ((10 * 1024) * 1024);
    } else {
        return false;
    }
}

List<int> SerializeResponse(protocol_spec.ResponsePacket packet) {
    final totalLen = (18 + option_4.defaultValue(0, option_4.map<List<int>, int>((List<int> v) => v.length, packet.Value))) + option_4.defaultValue(0, option_4.map<String, int>((String m) => encoding.get_UTF8().getBytes(m).length, packet.ErrorMessage));
    final buffer = List.filled(totalLen, 0);
    var offset = 0;
    binary.writeUInt32LittleEndian(packet.MagicNumber, buffer, offset);
    offset = offset + 4;
    binary.writeByte(packet.Version, buffer, offset);
    offset = offset + 1;
    binary.writeByte(packet.StatusCode, buffer, offset);
    offset = offset + 1;
    binary.writeInt32LittleEndian(packet.TotalPacketLength, buffer, offset);
    offset = offset + 4;
    binary.writeInt32LittleEndian(packet.ValueLength, buffer, offset);
    offset = offset + 4;
    binary.writeInt32LittleEndian(packet.ErrorMessageLength, buffer, offset);
    offset = offset + 4;
    final types.Some<List<int>>? matchValue = packet.Value;
    if (matchValue == null) {
    } else {
        final value_2 = types.value(matchValue);
        array.copyTo<int>(value_2, 0, buffer, offset, value_2.length);
        offset = offset + value_2.length;
    }
    final types.Some<String>? matchValue_1 = packet.ErrorMessage;
    if (matchValue_1 == null) {
    } else {
        final msgBytes = encoding.get_UTF8().getBytes(types.value(matchValue_1));
        array.copyTo<int>(msgBytes, 0, buffer, offset, msgBytes.length);
    }
    return buffer;
}

class ParseResult$1<$T> implements types.Union, Comparable<ParseResult$1<$T>> {
    final int tag;
    const ParseResult$1(this.tag);
    @override
    bool operator ==(Object other) => (other is ParseResult$1<$T>) && (other.tag == tag);
    @override
    int get hashCode => tag.hashCode;
    @override
    int compareTo(ParseResult$1<$T> other) => tag.compareTo(other.tag);
}

class ParseResult$1_ParseSuccess<$T> extends ParseResult$1<$T> {
    final $T Item;
    const ParseResult$1_ParseSuccess(this.Item): super(0);
    @override
    bool operator ==(Object other) => (other is ParseResult$1_ParseSuccess<$T>) && util.equalsDynamic(other.Item, Item);
    @override
    int get hashCode => util.combineHashCodes([tag.hashCode, Item.hashCode]);
    @override
    int compareTo(ParseResult$1<$T> other) {
        if (other is ParseResult$1_ParseSuccess<$T>) {
            return util.compareDynamic(Item, other.Item);
        } else {
            return tag.compareTo(other.tag);
        }
    }
}

class ParseResult$1_ParseError<$T> extends ParseResult$1<$T> {
    final String Item;
    const ParseResult$1_ParseError(this.Item): super(1);
    @override
    bool operator ==(Object other) => (other is ParseResult$1_ParseError<$T>) && (other.Item == Item);
    @override
    int get hashCode => util.combineHashCodes([tag.hashCode, Item.hashCode]);
    @override
    int compareTo(ParseResult$1<$T> other) {
        if (other is ParseResult$1_ParseError<$T>) {
            return Item.compareTo(other.Item);
        } else {
            return tag.compareTo(other.tag);
        }
    }
}

ParseResult$1<protocol_spec.ResponsePacket> DeserializeResponse(List<int> buffer) {
    try {
        if (buffer.length < 18) {
            return const ParseResult$1<protocol_spec.ResponsePacket>(/* InsufficientData */ 2);
        } else {
            var offset = 0;
            final magicNumber = binary.readUInt32LittleEndian(buffer, offset);
            offset = offset + 4;
            final version = binary.readByte(buffer, offset);
            offset = offset + 1;
            final statusCode = binary.readByte(buffer, offset);
            offset = offset + 1;
            final totalPacketLength = binary.readInt32LittleEndian(buffer, offset);
            offset = offset + 4;
            final valueLength = binary.readInt32LittleEndian(buffer, offset);
            offset = offset + 4;
            final errorMessageLength = binary.readInt32LittleEndian(buffer, offset);
            offset = offset + 4;
            if (buffer.length < totalPacketLength) {
                return const ParseResult$1<protocol_spec.ResponsePacket>(/* InsufficientData */ 2);
            } else if (!((magicNumber == 1397310287) && (version == 1))) {
                return const ParseResult$1_ParseError<protocol_spec.ResponsePacket>('Invalid header');
            } else if ((valueLength < 0) || (errorMessageLength < 0)) {
                return const ParseResult$1_ParseError<protocol_spec.ResponsePacket>('Invalid length field');
            } else if (totalPacketLength != ((18 + valueLength) + errorMessageLength)) {
                return const ParseResult$1_ParseError<protocol_spec.ResponsePacket>('Packet length mismatch');
            } else {
                final types.Some<List<int>>? value = (valueLength > 0) ? types.Some(array.getSubArray<int>(buffer, offset, valueLength)) : null;
                offset = offset + valueLength;
                final types.Some<String>? tmp_capture_2;
                if (errorMessageLength > 0) {
                    final errorBytes = array.getSubArray<int>(buffer, offset, errorMessageLength);
                    tmp_capture_2 = types.Some(encoding.get_UTF8().getString(errorBytes));
                } else {
                    tmp_capture_2 = null;
                }
                return ParseResult$1_ParseSuccess<protocol_spec.ResponsePacket>(protocol_spec.ResponsePacket(magicNumber, version, statusCode, totalPacketLength, valueLength, errorMessageLength, value, tmp_capture_2));
            }
        }
    } catch (ex) {
        final arg = ex.toString();
        return ParseResult$1_ParseError<protocol_spec.ResponsePacket>((string.toText(string.printf('Error parsing response: %s')))(arg));
    }
}

