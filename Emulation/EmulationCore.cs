using System;
using System.Threading;
using JetBrains.Annotations;


namespace CPU7Plus.Emulation {
    
    /**
    *  This logic is more or less a direct port of Chris Giorgi's centemu.c emulator.
     *
     * One of these days I will get around to running the actual C code via a DLL or something to that effect.
    */
    public class EmulationCore {

        private EmulationContext _context;

        public EmulationCore(EmulationContext context) {
            _context = context;
        }

        /**
         * Singles steps the processor
         */
        public bool Step() {
            int pc = Context.Pc;

            byte isr = Context.Fetch8(pc);

            // Misc Class Instruction
            if ((isr & 0xF0) == 0x00) {

                switch (isr & 0x0F) {
                    case 0: // HLT
                       //Context.Pc = ToUShort(pc + 1);
                        return false;
                    
                    case 1: // NOP
                        break;
                    
                    case 2: // FSF
                        _context.FlagF = true;
                        break;
                    
                    case 3: // FCF
                        _context.FlagF = false;
                        break;
                    
                    case 4: // EI
                        _context.FlagI = true;
                        break;
                    
                    case 5: // DI
                        _context.FlagI = false;
                        break;
                    
                    case 6: // FSL
                        _context.FlagL = true;
                        break;
                    
                    case 7: // FCL
                        _context.FlagL = false;
                        break;
                    
                    case 8: // FIL
                        _context.FlagL = !_context.FlagL;
                        break;
                    
                    case 10: // RETI
                    case 9: // RET
                        _context.Pc = _context.GetRegister16(2);
                        
                        // Pop off the top of stack
                        _context.SetRegister16(2, Context.Fetch16(Context.GetRegister16(5), false));
                        _context.SetRegister16(5, ToUShort(_context.GetRegister16(5) + 2));
                        return true;
                    
                    case 13: // PCX
                        Context.SetRegister16(2, ToUShort(Context.Pc + 1));
                        break;
                    
                    case 14: // DLY
                        Thread.Sleep(5);
                        break;

                }
                
                Context.Pc = ToUShort(pc + 1); 
            } 
            
            // Branch Class Instruction
            else if ((isr & 0xF0) == 0x10) {
                int offset = ToSignedOffset(Context.Fetch8(pc + 1));

                bool branch = false;
                bool pos = _context.FlagM != _context.FlagF;

                switch (isr & 0x0F) {
                    case 0: branch = _context.FlagL; break; // BL
                    case 1: branch = !_context.FlagL; break; // BNL
                    case 2: branch = _context.FlagF; break; // BF
                    case 3: branch = !_context.FlagF; break; // BNF
                    case 4: branch = _context.FlagV; break; // BZ
                    case 5: branch = !_context.FlagV; break; // BNZ
                    case 6: branch = _context.FlagM; break; // BM
                    case 7: branch = !_context.FlagM; break; // BP
                    case 8: branch = !_context.FlagM && !_context.FlagV; break; // BGZ
                    case 9: branch = _context.FlagM || _context.FlagV; break; // BLE
                    case 10: branch = Context.GetSenseSwitch(0); break; // BS1
                    case 11: branch = Context.GetSenseSwitch(0); break; // BS2
                    case 12: branch = Context.GetSenseSwitch(0); break; // BS3
                    case 13: branch = Context.GetSenseSwitch(0); break; // BS4
                }

                if (!branch) offset = 0;
                
                Context.Pc = ToUShort(pc + 2 + offset);
            }

            // Byte-Wise Single Operand Operations
            else if ((isr & 0xF0) == 0x20) {
                int length = 1;
                int reg;
                bool singlet = false;
                
                // Get register
                if ((isr & 0x08) == 0x08) {
                    reg = 1;
                    singlet = true;
                } else {
                    reg = Context.Fetch8(pc + 1) >> 4;
                    length = 2;
                }

                // Get inputs, outputs, and operation
                int inv = Context.GetRegister8(reg);
                int outv = 0;
                int op = isr & 0x07;

                if (op == 0 || op == 1) {
                    // INC / DEC
                    if (op == 0) outv = inv + 1;
                    else outv = inv - 1;

                    FlagV(ToByte(outv));
                    FlagM(ToByte(outv));
                    Context.FlagF = false;
                    if (op == 0 && outv == 0x80) Context.FlagF = true;
                    if (op == 1 && outv == 0x7F) Context.FlagF = true;
                } else if (op == 2) {
                    // CLR
                    outv = 0;
                    Context.FlagL = false;
                    Context.FlagM = false;
                    Context.FlagF = false;
                    Context.FlagV = true;
                } else if (op == 3) {
                    // NOT
                    outv = ~inv;
                    
                    FlagV(ToByte(outv));
                    FlagM(ToByte(outv));
                } else if (op == 4) {
                    // SRL
                    outv = inv >> 1;
                    outv |= inv & 0x80;
                    FlagM(ToByte(outv));
                    FlagV(ToByte(outv));
                    Context.FlagL = (inv & 0x01) == 0x01;
                } else if (op == 5) {
                    // SLL
                    outv = inv << 1;
                    FlagM(ToByte(outv));
                    FlagV(ToByte(outv));
                    Context.FlagL = (inv & 0x80) == 0x80;
                    Context.FlagF = Context.FlagL != Context.FlagM;
                } else if (op == 6 && !singlet) {
                    // RCC
                    outv = inv >> 1  | (Context.FlagL ? 0x80 : 0);
                    Context.FlagL = (inv & 0x01) == 0x01;
                    FlagM(ToByte(outv));
                    FlagV(ToByte(outv));
                } else if (op == 7 && !singlet) {
                    // RLC
                    outv = (inv << 1) | (Context.FlagL ? 0x01 : 0);
                    Context.FlagL = (inv & 0x80) == 0x80;
                    FlagM(ToByte(outv));
                    FlagV(ToByte(outv));
                    Context.FlagF = Context.FlagL != Context.FlagM;
                }

                if (singlet && op == 6) {
                    // MMU
                } else if (singlet && op == 7) {
                    // DMA

                    DoDmaOperation(Context.Fetch8(Context.Pc + 1));
                    length = 2;
                } else {

                    byte outb = ToByte(outv);

                    // Write register and advance
                    Context.SetRegister8(reg, outb);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // Word-Wise Single Operand Operations
            else if ((isr & 0xF0) == 0x30) {
                int length = 1;
                int reg;
                bool singlet = false;
                
                // Get register
                if ((isr & 0x08) == 0x08) {
                    reg = 0;
                    singlet = true;
                } else {
                    reg = Context.Fetch8(pc + 1) >> 5;
                    length = 2;
                }

                int outv = 0;
                int op = isr & 0x07;
                
                // Special operations
                if (singlet && op == 6) {
                    // INC RT
                    reg = 2;
                    op = 0;
                } else if (singlet && op == 7) {
                    // DEC RT
                    reg = 2;
                    op = 1;
                }

                // Get inputs, outputs, and operation
                int inv = Context.GetRegister16(reg);
                
                if (op == 0 || op == 1) {
                    // INC / DEC
                    if (op == 0) outv = inv + 1;
                    else outv = inv - 1;

                    FlagV(ToUShort(outv));
                    FlagM(ToUShort(outv));
                    Context.FlagF = false;
                    if (op == 0 && outv == 0x8000) Context.FlagF = true;
                    if (op == 1 && outv == 0x7FFF) Context.FlagF = true;
                } else if (op == 2) {
                    // CLR
                    outv = 0;
                    Context.FlagL = false;
                    Context.FlagM = false;
                    Context.FlagF = false;
                    Context.FlagV = true;
                } else if (op == 3) {
                    // NOT
                    outv = ~inv;
                    
                    FlagV(ToUShort(outv));
                    FlagM(ToUShort(outv));
                } else if (op == 4) {
                    // SRL
                    outv = inv >> 1;
                    outv |= inv & 0x8000;
                    FlagM(ToUShort(outv));
                    FlagV(ToUShort(outv));
                    Context.FlagL = (inv & 0x0001) == 0x0001;
                } else if (op == 5) {
                    // SLL
                    outv = inv << 1;
                    FlagM(ToUShort(outv));
                    FlagV(ToUShort(outv));
                    Context.FlagL = (inv & 0x8000) == 0x8000;
                    Context.FlagF = Context.FlagL != Context.FlagM;
                } else if (op == 6 && !singlet) {
                    // RCC
                    outv = inv >> 1  | (Context.FlagL ? 0x8000 : 0);
                    Context.FlagL = (inv & 0x0001) == 0x0001;
                    FlagM(ToUShort(outv));
                    FlagV(ToUShort(outv));
                } else if (op == 7 && !singlet) {
                    // RLC
                    outv = (inv << 1) | (Context.FlagL ? 0x0001 : 0);
                    Context.FlagL = (inv & 0x8000) == 0x8000;
                    FlagM(ToUShort(outv));
                    FlagV(ToUShort(outv));
                    Context.FlagF = Context.FlagL != Context.FlagM;
                }

                ushort outs = ToUShort(outv);

                // Write register and advance
                Context.SetRegister16(reg, outs);
                Context.Pc = ToUShort(pc + length);
            }
            
            // Byte-Wise Double Operand Operation
            else if ((isr & 0xF0) == 0x40) {
                int length = 1;
                int regDst;
                int regSrc;
                bool singlet = false;

                // Get register
                if ((isr & 0x08) == 0x08) {
                    regDst = 3;
                    regSrc = 1;
                    singlet = true;
                } else {
                    regSrc = Context.Fetch8(pc + 1) >> 4;
                    regDst = Context.Fetch8(pc + 1) & 0x0F;
                    length = 2;
                }
                
                // Get inputs, outputs, and operation
                int inDst = Context.GetRegister8(regDst);
                int inSrc = Context.GetRegister8(regSrc);
                int outv = 0;
                int op = isr & 0x07;

                if (op == 0) {
                    // ADD
                    outv = inDst + inSrc;

                    Context.FlagL = (outv > 255);
                    Context.FlagF = ((inDst & 0x80) == 0x80) == ((inSrc & 0x80) == 0x80) && ((outv & 0x80) == 0x80) != ((inDst & 0x80) == 0x80);
                    //Context.FlagF = ((ToSignedOffset(ToByte(inDst)) >= 0) == (ToSignedOffset(ToByte(inSrc)) >= 0)) && ((ToSignedOffset(ToByte(inSrc)) >= 0) != (ToSignedOffset(ToByte(outv)) >= 0));
                } else if (op == 1) {
                    // SUB
                    outv = inSrc - inDst;

                    Context.FlagL = ((inSrc + (~inDst & 0xFF) + 1) > 0);
                    Context.FlagF = ((inSrc & 0x80) == 0x80) != ((inDst & 0x80) == 0x80) && ((inDst & 0x80) == 0x80) == ((outv & 0x80) == 0x80);
                    //Context.FlagF = ((ToSignedOffset(ToByte(inSrc)) >= 0) != (ToSignedOffset(ToByte(inDst)) >= 0)) && ((ToSignedOffset(ToByte(inDst)) >= 0) == (ToSignedOffset(ToByte(outv)) >= 0));
                } else if (op == 2) {
                    // AND
                    outv = inDst & inSrc;
                } else if (op == 3) {
                    if (singlet) {
                        // MOV XL,AL
                        outv = inSrc;
                        regDst = 5;
                    } else {
                        // OR
                        outv = inDst | inSrc;
                    }
                } else if (op == 4) {

                    if (singlet) {
                        // MOV YL,AL
                        outv = inSrc;
                        regDst = 7;
                    } else {
                        // XOR
                        outv = inDst ^ inSrc;
                    }
                } else if (op == 5) {
                    // MOV
                    outv = inSrc;
                }  else if (singlet && op == 6) {
                    // MOV ZL,AL
                    outv = inSrc;
                    regDst = 9;
                }  else if (singlet && op == 7) {
                    // MOV SL,AL
                    outv = inSrc;
                    regDst = 11;
                }

                byte outb = ToByte(outv);
                
                // Set additional flags
                Context.FlagV = outb == 0;
                Context.FlagM = (outb & 0x80) == 0x80;

                // Write register and advance
                Context.SetRegister8(regDst, outb);
                Context.Pc = ToUShort(pc + length);
                
            }
            
            // Word-Wise Double Operand Operation
            else if ((isr & 0xF0) == 0x50) {
                int length = 1;
                int regDst;
                int regSrc;
                bool singlet = false;
                
                // Get register
                if ((isr & 0x08) == 0x08) {
                    regDst = 1;
                    regSrc = 0;
                    singlet = true;
                } else {
                    regSrc = Context.Fetch8(pc + 1) >> 5;
                    regDst = (Context.Fetch8(pc + 1) & 0x0F) >> 1;
                    length = 2;
                }
                
                // Get inputs, outputs, and operation
                int inDst = Context.GetRegister16(regDst);
                int inSrc = Context.GetRegister16(regSrc);
                int outv = 0;
                int op = isr & 0x07;
                
                if (op == 0) {
                    // ADD
                    outv = inDst + inSrc;

                    Context.FlagL = (outv > 65535);
                    Context.FlagF = ((inDst & 0x8000) == 0x8000) == ((inSrc & 0x8000) == 0x8000) && ((outv & 0x8000) == 0x8000) != ((inDst & 0x8000) == 0x8000);
                    //Context.FlagF = ((ToSignedWord(ToUShort(inDst)) >= 0) == (ToSignedWord(ToUShort(inSrc)) >= 0)) && ((ToSignedWord(ToUShort(inSrc)) >= 0) != (ToSignedWord(ToUShort(outv)) >= 0));
                } else if (op == 1) {
                    // SUB
                    outv = inSrc - inDst;

                    Context.FlagL = ((inSrc + (~inDst & 0xFFFF) + 1) > 0);
                    Context.FlagF = ((inSrc & 0x8000) == 0x8000) != ((inDst & 0x8000) == 0x8000) && ((inDst & 0x8000) == 0x8000) == ((outv & 0x8000) == 0x8000);
                    //Context.FlagF = ((ToSignedWord(ToUShort(inSrc)) >= 0) != (ToSignedWord(ToUShort(inDst)) >= 0)) && ((ToSignedWord(ToUShort(inSrc)) >= 0) == (ToSignedWord(ToUShort(outv)) >= 0));
                } else if (op == 2) {
                    // AND
                    outv = inDst & inSrc;
                } else if (op == 3) {
                    if (singlet) {
                        // MOV X,A
                        outv = inSrc;
                        regDst = 2;
                    } else {
                        // OR
                        outv = inDst | inSrc;
                    }
                } else if (op == 4) {
                    if (singlet) {
                        // MOV Y,A
                        outv = inSrc;
                        regDst = 3;
                    } else {
                        // XOR
                        outv = inDst ^ inSrc;
                    }
                } else if (op == 5) {
                    // MOV
                    outv = inSrc;
                } else if (singlet && op == 6) {
                    // MOV Z,A
                    outv = inSrc;
                    regDst = 4;
                }  else if (singlet && op == 7) {
                    // MOV S,A
                    outv = inSrc;
                    regDst = 5;
                }

                ushort outs = ToUShort(outv);
                
                // Set additional flags
                Context.FlagV = outs == 0;
                Context.FlagM = (outs & 0x8000) == 0x8000; 
                
                // Write register and advance
                Context.SetRegister16(regDst, outs);
                Context.Pc = ToUShort(pc + length);
                
            }
            
            // RT LD/ST Class Instruction
            else if ((isr & 0xF0) == 0x60) {

                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;

                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(2, value);
                    
                    // Set additional flags
                    LoadFlags(value);


                } else {
                    // ST
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;

                    ushort value = Context.GetRegister16(2);
                    Context.Store16(addr, value, false);
                    
                    //Console.Write("Storing " + value.ToString("X4") + " at ");
                    
                    // Set additional flags
                    StoreFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }


            // Jump/Call Class Instruction
            else if ((isr & 0xF0) == 0x70) {
                ushort destination = 0;

                // Get addressing mode
                int mode = isr & 0x07;
                int length = 1;

                (destination, length) = GetArgumentAddress(mode, pc, false);

                // Call instruction
                if ((isr & 0x08) == 0x08) {
                    
                                        
                    // Push onto the stack
                    _context.SetRegister16(5, ToUShort(_context.GetRegister16(5) - 2));
                    _context.Store16(_context.GetRegister16(5), _context.GetRegister16(2), false);
                    
                    _context.SetRegister16(2, ToUShort(pc + length));
                }

                Context.Pc = destination;
            }
            
            // AL LD Class Instruction
            else if ((isr & 0xF0) == 0x80) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, true);
                    if (op == 0) length = 2;

                    byte value = Context.Fetch8(addr);
                    Context.SetRegister8(1, value);

                    // Set additional flags
                    LoadFlags(value);
                    
                } else {
                    // LD [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    byte value = Context.Fetch8(addr);
                    Context.SetRegister8(1, value);

                    // Set additional flags
                    LoadFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // AX LD Class Instruction
            else if ((isr & 0xF0) == 0x90) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;
                    
                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(0, value);
                    
                    // Set additional flags
                    LoadFlags(value);
                    
                } else {
                    // LD [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(0, value);
                    
                    // Set additional flags
                    LoadFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // AL ST Class Instruction
            else if ((isr & 0xF0) == 0xA0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // ST [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, true);
                    if (op == 0) length = 2;

                    byte value = Context.GetRegister8(1);
                    Context.Store8(addr, value);
                    
                    // Set additional flags
                    StoreFlags(value);

                } else {
                    // ST [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    byte value = Context.GetRegister8(1);
                    Context.Store8(addr, value);
                    
                    // Set additional flags
                    StoreFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // AX ST Class Instruction
            else if ((isr & 0xF0) == 0xB0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // ST [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;

                    ushort value = Context.GetRegister16(0);
                    Context.Store16(addr, value, false);
                    
                    // Set additional flags
                    StoreFlags(value);

                } else {
                    // ST [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    ushort value = Context.GetRegister16(0);
                    Context.Store16(addr, value, false);
                    
                    // Set additional flags
                    StoreFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // BL LD Class Instruction
            else if ((isr & 0xF0) == 0xC0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, true);
                    if (op == 0) length = 2;
                    
                    byte value = Context.Fetch8(addr);
                    Context.SetRegister8(3, value);

                    // Set additional flags
                    LoadFlags(value);
                    
                } else {
                    // LD [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    byte value = Context.Fetch8(addr);
                    Context.SetRegister8(3, value);

                    // Set additional flags
                    LoadFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // BX LD Class Instruction
            else if ((isr & 0xF0) == 0xD0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // LD [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;
                    
                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(1, value);
                    
                    // Set additional flags
                    LoadFlags(value);
                    
                } else {
                    // LD [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    ushort value = Context.Fetch16(addr, false);
                    Context.SetRegister16(1, value);
                    
                    // Set additional flags
                    LoadFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // BL ST Class Instruction
            else if ((isr & 0xF0) == 0xE0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // ST [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, true);
                    if (op == 0) length = 2;
                    
                    byte value = Context.GetRegister8(3);
                    Context.Store8(addr, value);
                    
                    // Set additional flags
                    StoreFlags(value);

                } else {
                    // ST [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    byte value = Context.GetRegister8(3);
                    Context.Store8(addr, value);
                    
                    // Set additional flags
                    StoreFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // BX ST Class Instruction
            else if ((isr & 0xF0) == 0xF0) {
                
                int op = isr & 0x07;
                int length = 1;

                if ((isr & 0x08) != 0x08) {
                    // ST [ADDRESS]
                    ushort addr;
                    (addr, length) = GetArgumentAddress(op, pc, false);
                    if (op == 0) length = 3;
                    
                    ushort value = Context.GetRegister16(1);
                    Context.Store16(addr, value, false);
                    
                    // Set additional flags
                    StoreFlags(value);

                } else {
                    // ST [REG]
                    ushort addr = Context.GetRegister16(op);
                    length = 1;
                    
                    ushort value = Context.GetRegister16(1);
                    Context.Store16(addr, value, false);
                    
                    // Set additional flags
                    StoreFlags(value);
                }
                
                Context.Pc = ToUShort(pc + length);
            }
            
            // Unknown Instruction
            else {
                Context.Pc = ToUShort(pc + 1); 
            }

            return true;
        }

        /**
         * Updates the state of the flags for a load
         */
        private void LoadFlags(byte value) {
            FlagV(value);
            FlagM(value);
        }
        
        /**
         * Updates the state of the flags for a load
         */
        private void LoadFlags(ushort value) {
            FlagV(value);
            FlagM(value);
        }
        
        /**
         * Updates the state of the flags for a load
         */
        private void StoreFlags(byte value) {
            FlagV(value);
            FlagM(value);
        }
        
        /**
         * Updates the state of the flags for a load
         */
        private void StoreFlags(ushort value) {
            FlagV(value);
            FlagM(value);
        }


        /**
         * Updates the state of the M flag
         */
        private void FlagM(byte value) {
            Context.FlagM = (value & 0x80) == 0x80; 
        }
        
        /**
         * Updates the state of the M flag
         */
        private void FlagM(ushort value) {
            Context.FlagM = (value & 0x8000) == 0x8000; 
        }

        /**
         * Updates the state of the Z flag
         */
        private void FlagV(byte value) {
            Context.FlagV = value == 0;
        }
        
        /**
         * Updates the state of the Z flag
         */
        private void FlagV(ushort value) {
            Context.FlagV = value == 0;
        }


        /**
         * Gets the address for an address-argument instruction (JUMP, CALL, LD, ST)
         */
        private (ushort, int) GetArgumentAddress(int mode, int pc, bool single) {

            int address;
            int length = 1;

            // Gets the address depending on the mode
            
            switch (mode) {
                case 1:
                    // Absolute addressing
                    address = Context.Fetch16(pc + 1, false);
                    length = 3;
                    break;
                        
                case 2:
                    // Indirect addressing
                    address = Context.Fetch16(Context.Fetch16(pc + 1, false), false);
                    length = 3;
                    break;
                    
                case 3:
                    // PC absolute addressing
                    address = pc + ToSignedOffset(Context.Fetch8(pc + 1)) + 2;
                    length = 2;
                    break;
                    
                case 4:
                    // PC indirect addressing
                    address = Context.Fetch16(ToUShort(pc + ToSignedOffset(Context.Fetch8(pc + 1)) + 2), false);
                    length = 2;
                    break;
                    
                case 5:
                    // Index addressing
                    address = GetIndexedAddr(pc, single);
                    length = 2;
                    if ((Context.Fetch8(pc + 1) & 0x08) == 0x08) length++;
                    break;
                    
                default:
                    address = pc + 1;
                    break;
            }

            return (ToUShort(address), length);
        }

        /**
         * Does addressing for indexed operations
         */
        private ushort GetIndexedAddr(int pc, bool single) {

            // Index mode
            byte imode = Context.Fetch8(pc + 1);
            // Get register
            int reg = (imode % 0xF0) >> 5;

            int address = 0;
            if ((imode & 0x03) == 0x01) {
                // Increment mode
                address = Context.GetRegister16(reg);
                Context.SetRegister16(reg, ToUShort(address + (single ? 1 : 2)));
            } else if ((imode & 0x03) == 0x02) {
                // Decrement mode
                address = Context.GetRegister16(reg) - (single ? 1 : 2);
                Context.SetRegister16(reg, ToUShort(address));
            } else {
                // Neutral Mode
                address = Context.GetRegister16(reg);
            }

            // Check for offset
            if ((imode & 0x08) == 0x08) {
                address += ToSignedOffset(Context.Fetch8(pc + 2));
            }
            
            // Index address mode
            if ((imode & 0x04) == 0x04) {
                address = Context.Fetch16(ToUShort(address), false);
            }

            return ToUShort(address);
        }

        /**
         * Does a DMA operation
         *
         * Based off of EtchedPixel's implemention
         */
        private void DoDmaOperation(byte arg) {
            int reg = (arg >> 5) & 0x07;
            int op = arg & 0x0F;

            Console.WriteLine("DMA Operation " + op + " On Reg " + reg);
            
            if (op == 0) {
                Context.DmaAddr = Context.GetRegister16(reg);
            } else if (op == 1) {
                Context.SetRegister16(reg, Context.DmaAddr);
            } else if (op == 2) {
                Context.DmaCount = Context.GetRegister16(reg);
            } else if (op == 3) {
                Context.SetRegister16(reg, Context.DmaCount);
            } else if (op == 4) {
                Context.DmaMode = ToByte((arg >> 4) & 0x0F);
            } else if (op == 6) {
                Context.DmaEnable = true;
            } else {
                //Console.WriteLine("Unknown DMA Operation!");
            }

        }

        /**
         * Turns an int into a UShort with bounding
         */
        private static ushort ToUShort(int i) {
            return Convert.ToUInt16(i & 0xFFFF);
        }

        /**
         * Turns an int into a Byte with bounding
         */
        private static byte ToByte(int i) {
            return Convert.ToByte(i & 0xFF);
        }

        /**
         * Converts an unsigned byte into a signed offset
         */
        private static int ToSignedOffset(byte b) {
            if (b >= 128) return b - 256;
            return b;
        }
        
        /**
         * Converts an unsigned byte into a signed word
         */
        private static int ToSignedWord(ushort w) {
            if (w >= 32768) return w - 65536;
            return w;
        }

        [NotNull]
        public EmulationContext Context {
            get => _context;
            set => _context = value ?? throw new ArgumentNullException(nameof(value));
        }
    }
}