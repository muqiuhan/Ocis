import { ResponsePacket, RequestPacket } from "../ProtocolSpec.js";
import { toInt32, toUInt32, getBytesInt32, getBytesUInt32 } from "./fable_modules/fable-library-js.4.25.0/BitConverter.js";
import { getSubArray, item, concat } from "./fable_modules/fable-library-js.4.25.0/Array.js";
import { Union } from "./fable_modules/fable-library-js.4.25.0/Types.js";
import { union_type, string_type } from "./fable_modules/fable-library-js.4.25.0/Reflection.js";
import { printf, toText } from "./fable_modules/fable-library-js.4.25.0/String.js";
import { get_UTF8 } from "./fable_modules/fable-library-js.4.25.0/Encoding.js";

/**
 * create request packet
 */
export function CreateRequest(commandType, key, value) {
    let valueLen;
    if (value == null) {
        valueLen = 0;
    }
    else {
        const v = value;
        valueLen = v.length;
    }
    return new RequestPacket(1397310287, 1, commandType, (18 + key.length) + valueLen, key.length, valueLen, key, value);
}

/**
 * serialize request packet to bytes
 */
export function SerializeRequest(packet) {
    const parts = [];
    void (parts.push(getBytesUInt32(packet.MagicNumber)));
    void (parts.push(new Uint8Array([packet.Version])));
    void (parts.push(new Uint8Array([packet.CommandType & 0xFF])));
    void (parts.push(getBytesInt32(packet.TotalPacketLength)));
    void (parts.push(getBytesInt32(packet.KeyLength)));
    void (parts.push(getBytesInt32(packet.ValueLength)));
    void (parts.push(packet.Key));
    const matchValue = packet.Value;
    if (matchValue == null) {
    }
    else {
        const value = matchValue;
        void (parts.push(value));
    }
    return concat(parts.slice(), Uint8Array);
}

export class ParseResult$1 extends Union {
    constructor(tag, fields) {
        super();
        this.tag = tag;
        this.fields = fields;
    }
    cases() {
        return ["ParseSuccess", "ParseError", "InsufficientData"];
    }
}

export function ParseResult$1_$reflection(gen0) {
    return union_type("Ocis.Protocol.ParseResult`1", [gen0], ParseResult$1, () => [[["Item", gen0]], [["Item", string_type]], []]);
}

/**
 * deserialize response from bytes
 */
export function DeserializeResponse(buffer) {
    let arg, errorBytes;
    try {
        if (buffer.length < 18) {
            return new ParseResult$1(2, []);
        }
        else {
            let offset = 0;
            const magicNumber = toUInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const version = item(offset, buffer);
            offset = ((offset + 1) | 0);
            const statusCode = item(offset, buffer);
            offset = ((offset + 1) | 0);
            const totalPacketLength = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const valueLength = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const errorMessageLength = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            if (buffer.length < totalPacketLength) {
                return new ParseResult$1(2, []);
            }
            else if (!((magicNumber === 1397310287) && (version === 1))) {
                return new ParseResult$1(1, ["invalid header"]);
            }
            else if ((valueLength < 0) ? true : (errorMessageLength < 0)) {
                return new ParseResult$1(1, ["invalid length field"]);
            }
            else if (totalPacketLength !== ((18 + valueLength) + errorMessageLength)) {
                return new ParseResult$1(1, ["packet length mismatch"]);
            }
            else {
                const value = (valueLength > 0) ? getSubArray(buffer, offset, valueLength) : undefined;
                offset = ((offset + valueLength) | 0);
                return new ParseResult$1(0, [new ResponsePacket(magicNumber, version, statusCode, totalPacketLength, valueLength, errorMessageLength, value, (errorMessageLength > 0) ? ((errorBytes = getSubArray(buffer, offset, errorMessageLength), get_UTF8().getString(errorBytes))) : undefined)]);
            }
        }
    }
    catch (ex) {
        return new ParseResult$1(1, [(arg = ex.message, toText(printf("error parsing response: %s"))(arg))]);
    }
}

/**
 * convert string to bytes
 */
export function ProtocolHelper_stringToBytes(s) {
    return get_UTF8().getBytes(s);
}

/**
 * convert bytes to string
 */
export function ProtocolHelper_bytesToString(bytes) {
    return get_UTF8().getString(bytes);
}

/**
 * create SET request
 */
export function ProtocolHelper_createSetRequest(key, value) {
    return CreateRequest(1, ProtocolHelper_stringToBytes(key), ProtocolHelper_stringToBytes(value));
}

/**
 * create GET request
 */
export function ProtocolHelper_createGetRequest(key) {
    return CreateRequest(2, ProtocolHelper_stringToBytes(key), undefined);
}

/**
 * create DELETE request
 */
export function ProtocolHelper_createDeleteRequest(key) {
    return CreateRequest(3, ProtocolHelper_stringToBytes(key), undefined);
}

