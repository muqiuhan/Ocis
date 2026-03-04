// ignore_for_file: camel_case_types, constant_identifier_names, non_constant_identifier_names, unnecessary_this
import './Ocis.Server/ProtocolSpec.dart' as protocol_spec;
import './fable_modules/fable_library/Encoding.dart' as encoding;
import './fable_modules/fable_library/Option.dart' as option_2;
import './fable_modules/fable_library/Types.dart' as types;
import './Protocol.dart' as protocol;

protocol_spec.RequestPacket createPacket(int commandType, String key, types.Some<List<int>>? value) {
    final keyBytes = encoding.get_UTF8().getBytes(key);
    final keyLen = keyBytes.length;
    final valueLen = option_2.defaultValue(0, option_2.map<List<int>, int>((List<int> v) => v.length, value));
    return protocol_spec.RequestPacket(1397310287, 1, commandType, (18 + keyLen) + valueLen, keyLen, valueLen, keyBytes, value);
}

List<int> createSetRequest(String key, List<int> value) => protocol.SerializeRequest(createPacket(1, key, types.Some(value)));

List<int> createGetRequest(String key) => protocol.SerializeRequest(createPacket(2, key, null));

List<int> createDeleteRequest(String key) => protocol.SerializeRequest(createPacket(3, key, null));

