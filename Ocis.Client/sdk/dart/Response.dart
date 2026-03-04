// ignore_for_file: camel_case_types, constant_identifier_names, non_constant_identifier_names, unnecessary_this
import './Ocis.Server/ProtocolSpec.dart' as protocol_spec;
import './fable_modules/fable_library/Option.dart' as option_1;
import './fable_modules/fable_library/Types.dart' as types;
import './fable_modules/fable_library/Util.dart' as util;
import './Protocol.dart' as protocol;

class ClientResult$1<$T> implements types.Union, Comparable<ClientResult$1<$T>> {
    final int tag;
    const ClientResult$1(this.tag);
    @override
    bool operator ==(Object other) => (other is ClientResult$1<$T>) && (other.tag == tag);
    @override
    int get hashCode => tag.hashCode;
    @override
    int compareTo(ClientResult$1<$T> other) => tag.compareTo(other.tag);
}

class ClientResult$1_Success<$T> extends ClientResult$1<$T> {
    final $T Item;
    const ClientResult$1_Success(this.Item): super(0);
    @override
    bool operator ==(Object other) => (other is ClientResult$1_Success<$T>) && util.equalsDynamic(other.Item, Item);
    @override
    int get hashCode => util.combineHashCodes([tag.hashCode, Item.hashCode]);
    @override
    int compareTo(ClientResult$1<$T> other) {
        if (other is ClientResult$1_Success<$T>) {
            return util.compareDynamic(Item, other.Item);
        } else {
            return tag.compareTo(other.tag);
        }
    }
}

class ClientResult$1_Error<$T> extends ClientResult$1<$T> {
    final String Item;
    const ClientResult$1_Error(this.Item): super(2);
    @override
    bool operator ==(Object other) => (other is ClientResult$1_Error<$T>) && (other.Item == Item);
    @override
    int get hashCode => util.combineHashCodes([tag.hashCode, Item.hashCode]);
    @override
    int compareTo(ClientResult$1<$T> other) {
        if (other is ClientResult$1_Error<$T>) {
            return Item.compareTo(other.Item);
        } else {
            return tag.compareTo(other.tag);
        }
    }
}

protocol.ParseResult$1<protocol_spec.ResponsePacket> parseResponse(List<int> bytes) => protocol.DeserializeResponse(bytes);

ClientResult$1<void> toClientResult(protocol.ParseResult$1<protocol_spec.ResponsePacket> parseResult) {
    switch (parseResult.tag) {
        case 1:
            final parseResult_1 = parseResult as protocol.ParseResult$1_ParseError<protocol_spec.ResponsePacket>;
            return ClientResult$1_Error<void>(parseResult_1.Item);
        case 2:
            return const ClientResult$1_Error<void>('Insufficient data');
        default:
            final parseResult_2 = parseResult as protocol.ParseResult$1_ParseSuccess<protocol_spec.ResponsePacket>;
            final response = parseResult_2.Item;
            final matchValue = response.StatusCode;
            if (matchValue == 0) {
                return const ClientResult$1_Success<void>(null);
            } else if (matchValue == 1) {
                return const ClientResult$1<void>(/* NotFound */ 1);
            } else if (matchValue == 2) {
                return ClientResult$1_Error<void>(option_1.defaultValue('Unknown error', response.ErrorMessage));
            } else {
                return const ClientResult$1_Error<void>('Invalid status code');
            }
            ;
    }
}

ClientResult$1<List<int>> toClientResultValue(protocol.ParseResult$1<protocol_spec.ResponsePacket> parseResult) {
    switch (parseResult.tag) {
        case 1:
            final parseResult_1 = parseResult as protocol.ParseResult$1_ParseError<protocol_spec.ResponsePacket>;
            return ClientResult$1_Error<List<int>>(parseResult_1.Item);
        case 2:
            return const ClientResult$1_Error<List<int>>('Insufficient data');
        default:
            final parseResult_2 = parseResult as protocol.ParseResult$1_ParseSuccess<protocol_spec.ResponsePacket>;
            final response = parseResult_2.Item;
            final matchValue = response.StatusCode;
            if (matchValue == 0) {
                final types.Some<List<int>>? matchValue_1 = response.Value;
                if (matchValue_1 == null) {
                    return const ClientResult$1_Error<List<int>>('Success response missing value');
                } else {
                    return ClientResult$1_Success<List<int>>(types.value(matchValue_1));
                }
            } else if (matchValue == 1) {
                return const ClientResult$1<List<int>>(/* NotFound */ 1);
            } else if (matchValue == 2) {
                return ClientResult$1_Error<List<int>>(option_1.defaultValue('Unknown error', response.ErrorMessage));
            } else {
                return const ClientResult$1_Error<List<int>>('Invalid status code');
            }
            ;
    }
}

