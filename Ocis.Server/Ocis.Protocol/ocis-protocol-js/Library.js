import { getBytesInt32, getBytesUInt32, toInt32, toUInt32 } from "./fable_modules/fable-library-js.4.25.0/BitConverter.js";
import { concat, getSubArray, item } from "./fable_modules/fable-library-js.4.25.0/Array.js";
import { RequestPacket } from "../ProtocolSpec.js";

/**
 * Parse request packet header from byte array
 */
export function TryParseRequestHeader(buffer) {
    if (buffer.length < 18) {
        return undefined;
    }
    else {
        try {
            let offset = 0;
            const magicNumber = toUInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const version = item(offset, buffer);
            offset = ((offset + 1) | 0);
            const commandType = ~~item(offset, buffer) | 0;
            offset = ((offset + 1) | 0);
            const totalPacketLength = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            const keyLength = toInt32(buffer, offset);
            offset = ((offset + 4) | 0);
            return ((magicNumber === 1397310287) && (version === 1)) ? (new RequestPacket(magicNumber, version, commandType, totalPacketLength, keyLength, toInt32(buffer, offset), new Uint8Array([]), undefined)) : undefined;
        }
        catch (matchValue) {
            return undefined;
        }
    }
}

/**
 * Parse complete request packet from byte array
 */
export function TryParseRequestPacket(buffer) {
    const matchValue = TryParseRequestHeader(buffer);
    if (matchValue == null) {
        return undefined;
    }
    else {
        const header = matchValue;
        if (buffer.length >= header.TotalPacketLength) {
            try {
                let offset = 18;
                const key = getSubArray(buffer, offset, header.KeyLength);
                offset = ((offset + header.KeyLength) | 0);
                return new RequestPacket(header.MagicNumber, header.Version, header.CommandType, header.TotalPacketLength, header.KeyLength, header.ValueLength, key, (header.ValueLength > 0) ? getSubArray(buffer, offset, header.ValueLength) : undefined);
            }
            catch (matchValue_1) {
                return undefined;
            }
        }
        else {
            return undefined;
        }
    }
}

/**
 * Create request packet
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
 * Serialize request packet to byte array
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

