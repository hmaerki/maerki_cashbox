CAMT053 beinhaltet den Kontoauszug ohne VESR-Information
CAMT054 beinhaltet die VESR-Information, aber NUR die VESR-Zahlungen

==> Beide Files sind mächtig. Zum Beispiel ist das Absenderkonto in keinem anderen File ersichtlich.
==> Nachteil: Es müssen zwei Files heruntergeladen werden.
==> Nachteil: Noch nicht implementiert

camt053_20180119155043.xml
<Ntry>
   <Amt Ccy="CHF">409.5</Amt>
   <CdtDbtInd>CRDT</CdtDbtInd>
   <RvslInd>false</RvslInd>
   <Sts>BOOK</Sts>
   <BookgDt>
      <Dt>2018-01-03</Dt>
   </BookgDt>
   <ValDt>
      <Dt>2018-01-03</Dt>
   </ValDt>
   <AcctSvcrRef>27056849</AcctSvcrRef>
   <BkTxCd>
      <Domn>
         <Cd>PMNT</Cd>
         <Fmly>
            <Cd>RCDT</Cd>
            <SubFmlyCd>VCOM</SubFmlyCd>
         </Fmly>
      </Domn>
      <Prtry>
         <Cd>1000</Cd>
      </Prtry>
   </BkTxCd>
   <AddtlNtryInf>Gutschrift VESR</AddtlNtryInf>
</Ntry>

Der Pointer auf das VESR-File: <AcctSvcrRef>27056849</AcctSvcrRef>

camt054_esr_20180119160446.xml
<NtryDtls>
   <TxDtls>
      <Refs>
         <AcctSvcrRef>27056849</AcctSvcrRef>
         <EndToEndId>NOTPROVIDED</EndToEndId>
         <Prtry>
            <Tp>01</Tp>
            <Ref>9999  9999</Ref>
         </Prtry>
      </Refs>
      <Amt Ccy="CHF">409.5</Amt>
      <CdtDbtInd>CRDT</CdtDbtInd>
      <BkTxCd>
         <Domn>
            <Cd>PMNT</Cd>
            <Fmly>
               <Cd>RCDT</Cd>
               <SubFmlyCd>VCOM</SubFmlyCd>
            </Fmly>
         </Domn>
         <Prtry>
            <Cd>1000</Cd>
         </Prtry>
      </BkTxCd>
      <Chrgs>
         <TtlChrgsAndTaxAmt Ccy="CHF">0</TtlChrgsAndTaxAmt>
      </Chrgs>
      <RltdPties>
         <Dbtr>
            <Nm>NOTPROVIDED</Nm>
         </Dbtr>
      </RltdPties>
      <RmtInf>
         <Strd>
            <CdtrRefInf>
               <Tp>
                  <CdOrPrtry>
                     <Prtry>ISR Reference</Prtry>
                  </CdOrPrtry>
               </Tp>
               <Ref>000282263814810000000225277</Ref>
            </CdtrRefInf>
         </Strd>
      </RmtInf>
      <RltdDts>
         <AccptncDtTm>2018-01-03T00:00:00.000+01:00</AccptncDtTm>
      </RltdDts>
   </TxDtls>
</NtryDtls>
</Ntry>