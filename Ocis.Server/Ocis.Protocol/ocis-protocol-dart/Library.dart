// ignore_for_file: camel_case_types, constant_identifier_names, non_constant_identifier_names, unnecessary_this
import '../ProtocolSpec.dart' as protocol_spec;
import './fable_modules/fable_library/Array.dart' as array;
import './fable_modules/fable_library/BitConverter.dart' as bit_converter;
import './fable_modules/fable_library/Encoding.dart' as encoding;
import './fable_modules/fable_library/String.dart' as string;
import './fable_modules/fable_library/Types.dart' as types;
import './fable_modules/fable_library/Util.dart' as util;

protocol_spec.RequestPacket CreateRequest(int commandType, List<int> key, types.Some<List<int>>? value) {
    final valueLen = (value == null) ? 0 : types.value(value).length;
    return protocol_spec.RequestPacket(1397310287, 1, commandType, (18 + key.length) + valueLen, key.length, valueLen, key, value);
}

List<int> SerializeRequest(protocol_spec.RequestPacket packet) {
    final parts = <List<int>>[];
    parts.add(bit_converter.getBytesUInt32(packet.MagicNumber));
    parts.add([packet.Version]);
    parts.add([packet.CommandType]);
    parts.add(bit_converter.getBytesInt32(packet.TotalPacketLength));
    parts.add(bit_converter.getBytesInt32(packet.KeyLength));
    parts.add(bit_converter.getBytesInt32(packet.ValueLength));
    parts.add(packet.Key);
    final types.Some<List<int>>? matchValue = packet.Value;
    if (matchValue == null) {
    } else {
        parts.add(types.value(matchValue));
    }
    return array.concat<int>(parts.sublist(0));
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
            final magicNumber = bit_converter.toUInt32(buffer, offset);
            offset = offset + 4;
            final version = buffer[offset];
            offset = offset + 1;
            final statusCode = buffer[offset];
            offset = offset + 1;
            final totalPacketLength = bit_converter.toInt32(buffer, offset);
            offset = offset + 4;
            final valueLength = bit_converter.toInt32(buffer, offset);
            offset = offset + 4;
            final errorMessageLength = bit_converter.toInt32(buffer, offset);
            offset = offset + 4;
            if (buffer.length < totalPacketLength) {
                return const ParseResult$1<protocol_spec.ResponsePacket>(/* InsufficientData */ 2);
            } else if (!((magicNumber == 1397310287) && (version == 1))) {
                return const ParseResult$1_ParseError<protocol_spec.ResponsePacket>('invalid header');
            } else if ((valueLength < 0) || (errorMessageLength < 0)) {
                return const ParseResult$1_ParseError<protocol_spec.ResponsePacket>('invalid length field');
            } else if (totalPacketLength != ((18 + valueLength) + errorMessageLength)) {
                return const ParseResult$1_ParseError<protocol_spec.ResponsePacket>('packet length mismatch');
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
        return ParseResult$1_ParseError<protocol_spec.ResponsePacket>((string.toText(string.printf('error parsing response: %s')))(arg));
    }
}

List<int> ProtocolHelper_stringToBytes(String s) => encoding.get_UTF8().getBytes(s);

String ProtocolHelper_bytesToString(List<int> bytes) => encoding.get_UTF8().getString(bytes);

protocol_spec.RequestPacket ProtocolHelper_createSetRequest(String key, String value) => CreateRequest(1, ProtocolHelper_stringToBytes(key), types.Some(ProtocolHelper_stringToBytes(value)));

protocol_spec.RequestPacket ProtocolHelper_createGetRequest(String key) => CreateRequest(2, ProtocolHelper_stringToBytes(key), null);

protocol_spec.RequestPacket ProtocolHelper_createDeleteRequest(String key) => CreateRequest(3, ProtocolHelper_stringToBytes(key), null);

