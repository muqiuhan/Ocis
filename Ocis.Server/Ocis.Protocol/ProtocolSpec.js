import { Record } from "./ocis-protocol-js/fable_modules/fable-library-js.4.25.0/Types.js";
import { string_type, record_type, option_type, array_type, enum_type, int32_type, uint8_type, uint32_type } from "./ocis-protocol-js/fable_modules/fable-library-js.4.25.0/Reflection.js";

export class RequestPacket extends Record {
    constructor(MagicNumber, Version, CommandType, TotalPacketLength, KeyLength, ValueLength, Key, Value) {
        super();
        this.MagicNumber = MagicNumber;
        this.Version = Version;
        this.CommandType = (CommandType | 0);
        this.TotalPacketLength = (TotalPacketLength | 0);
        this.KeyLength = (KeyLength | 0);
        this.ValueLength = (ValueLength | 0);
        this.Key = Key;
        this.Value = Value;
    }
}

export function RequestPacket_$reflection() {
    return record_type("Ocis.Server.ProtocolSpec.RequestPacket", [], RequestPacket, () => [["MagicNumber", uint32_type], ["Version", uint8_type], ["CommandType", enum_type("Ocis.Server.ProtocolSpec.CommandType", int32_type, [["Set", 1], ["Get", 2], ["Delete", 3]])], ["TotalPacketLength", int32_type], ["KeyLength", int32_type], ["ValueLength", int32_type], ["Key", array_type(uint8_type)], ["Value", option_type(array_type(uint8_type))]]);
}

export class ResponsePacket extends Record {
    constructor(MagicNumber, Version, StatusCode, TotalPacketLength, ValueLength, ErrorMessageLength, Value, ErrorMessage) {
        super();
        this.MagicNumber = MagicNumber;
        this.Version = Version;
        this.StatusCode = StatusCode;
        this.TotalPacketLength = (TotalPacketLength | 0);
        this.ValueLength = (ValueLength | 0);
        this.ErrorMessageLength = (ErrorMessageLength | 0);
        this.Value = Value;
        this.ErrorMessage = ErrorMessage;
    }
}

export function ResponsePacket_$reflection() {
    return record_type("Ocis.Server.ProtocolSpec.ResponsePacket", [], ResponsePacket, () => [["MagicNumber", uint32_type], ["Version", uint8_type], ["StatusCode", enum_type("Ocis.Server.ProtocolSpec.StatusCode", uint8_type, [["Success", 0], ["NotFound", 1], ["Error", 2]])], ["TotalPacketLength", int32_type], ["ValueLength", int32_type], ["ErrorMessageLength", int32_type], ["Value", option_type(array_type(uint8_type))], ["ErrorMessage", option_type(string_type)]]);
}

