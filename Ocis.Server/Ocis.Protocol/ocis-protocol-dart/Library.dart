// ignore_for_file: camel_case_types, constant_identifier_names, non_constant_identifier_names, unnecessary_this
import '../ProtocolSpec.dart' as protocol_spec;
import './fable_modules/fable_library/Array.dart' as array;
import './fable_modules/fable_library/BitConverter.dart' as bit_converter;
import './fable_modules/fable_library/Types.dart' as types;

types.Some<protocol_spec.RequestPacket>? TryParseRequestHeader(List<int> buffer) {
    if (buffer.length < 18) {
        return null;
    } else {
        try {
            var offset = 0;
            final magicNumber = bit_converter.toUInt32(buffer, offset);
            offset = offset + 4;
            final version = buffer[offset];
            offset = offset + 1;
            final commandType = buffer[offset];
            offset = offset + 1;
            final totalPacketLength = bit_converter.toInt32(buffer, offset);
            offset = offset + 4;
            final keyLength = bit_converter.toInt32(buffer, offset);
            offset = offset + 4;
            if ((magicNumber == 1397310287) && (version == 1)) {
                return types.Some(protocol_spec.RequestPacket(magicNumber, version, commandType, totalPacketLength, keyLength, bit_converter.toInt32(buffer, offset), <int>[], null));
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
                var offset = 18;
                final key = array.getSubArray<int>(buffer, offset, header.KeyLength);
                offset = offset + header.KeyLength;
                return types.Some(protocol_spec.RequestPacket(header.MagicNumber, header.Version, header.CommandType, header.TotalPacketLength, header.KeyLength, header.ValueLength, key, (header.ValueLength > 0) ? types.Some(array.getSubArray<int>(buffer, offset, header.ValueLength)) : null));
            } catch (matchValue_1) {
                return null;
            }
        } else {
            return null;
        }
    }
}

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

