// ignore_for_file: camel_case_types, constant_identifier_names, non_constant_identifier_names, unnecessary_this
import './ocis-protocol-dart/fable_modules/fable_library/Types.dart' as types;
import './ocis-protocol-dart/fable_modules/fable_library/Util.dart' as util;

class RequestPacket implements types.Record, Comparable<RequestPacket> {
    final int MagicNumber;
    final int Version;
    final int CommandType;
    final int TotalPacketLength;
    final int KeyLength;
    final int ValueLength;
    final List<int> Key;
    final types.Some<List<int>>? Value;
    const RequestPacket(this.MagicNumber, this.Version, this.CommandType, this.TotalPacketLength, this.KeyLength, this.ValueLength, this.Key, this.Value);
    @override
    bool operator ==(Object other) => (other is RequestPacket) && ((other.MagicNumber == MagicNumber) && ((other.Version == Version) && ((other.CommandType == CommandType) && ((other.TotalPacketLength == TotalPacketLength) && ((other.KeyLength == KeyLength) && ((other.ValueLength == ValueLength) && (util.equalsList(other.Key, Key, (int x, int y) => x == y) && (other.Value == Value))))))));
    @override
    int get hashCode => util.combineHashCodes([MagicNumber.hashCode, Version.hashCode, CommandType.hashCode, TotalPacketLength.hashCode, KeyLength.hashCode, ValueLength.hashCode, Key.hashCode, Value.hashCode]);
    @override
    int compareTo(RequestPacket other) {
        late int $r;
        if (($r = MagicNumber.compareTo(other.MagicNumber)) == 0) {
            if (($r = Version.compareTo(other.Version)) == 0) {
                if (($r = CommandType.compareTo(other.CommandType)) == 0) {
                    if (($r = TotalPacketLength.compareTo(other.TotalPacketLength)) == 0) {
                        if (($r = KeyLength.compareTo(other.KeyLength)) == 0) {
                            if (($r = ValueLength.compareTo(other.ValueLength)) == 0) {
                                if (($r = util.compareList(Key, other.Key, (int x, int y) => x.compareTo(y))) == 0) {
                                    $r = util.compareNullable(Value, other.Value, (types.Some<List<int>> x, types.Some<List<int>> y) => x.compareTo(y));
                                }
                            }
                        }
                    }
                }
            }
        }
        return $r;
    }
}

class ResponsePacket implements types.Record, Comparable<ResponsePacket> {
    final int MagicNumber;
    final int Version;
    final int StatusCode;
    final int TotalPacketLength;
    final int ValueLength;
    final int ErrorMessageLength;
    final types.Some<List<int>>? Value;
    final types.Some<String>? ErrorMessage;
    const ResponsePacket(this.MagicNumber, this.Version, this.StatusCode, this.TotalPacketLength, this.ValueLength, this.ErrorMessageLength, this.Value, this.ErrorMessage);
    @override
    bool operator ==(Object other) => (other is ResponsePacket) && ((other.MagicNumber == MagicNumber) && ((other.Version == Version) && ((other.StatusCode == StatusCode) && ((other.TotalPacketLength == TotalPacketLength) && ((other.ValueLength == ValueLength) && ((other.ErrorMessageLength == ErrorMessageLength) && ((other.Value == Value) && (other.ErrorMessage == ErrorMessage))))))));
    @override
    int get hashCode => util.combineHashCodes([MagicNumber.hashCode, Version.hashCode, StatusCode.hashCode, TotalPacketLength.hashCode, ValueLength.hashCode, ErrorMessageLength.hashCode, Value.hashCode, ErrorMessage.hashCode]);
    @override
    int compareTo(ResponsePacket other) {
        late int $r;
        if (($r = MagicNumber.compareTo(other.MagicNumber)) == 0) {
            if (($r = Version.compareTo(other.Version)) == 0) {
                if (($r = StatusCode.compareTo(other.StatusCode)) == 0) {
                    if (($r = TotalPacketLength.compareTo(other.TotalPacketLength)) == 0) {
                        if (($r = ValueLength.compareTo(other.ValueLength)) == 0) {
                            if (($r = ErrorMessageLength.compareTo(other.ErrorMessageLength)) == 0) {
                                if (($r = util.compareNullable(Value, other.Value, (types.Some<List<int>> x, types.Some<List<int>> y) => x.compareTo(y))) == 0) {
                                    $r = util.compareNullable(ErrorMessage, other.ErrorMessage, (types.Some<String> x, types.Some<String> y) => x.compareTo(y));
                                }
                            }
                        }
                    }
                }
            }
        }
        return $r;
    }
}

