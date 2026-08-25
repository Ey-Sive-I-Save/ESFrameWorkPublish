# Evidence receipt contract

Every formal Knowledge Validator receipt must satisfy the project strict evidence contract and include `skillName`, `case`, `status`, `evidenceLevel`, `receiptPath`, `sourceRefs`, `timestampUtc`, `skillHash`, `governanceHash`, `validatorHash`, `planHash`, and `sourceRefHashes`.

The receipt identifies a particular validation run. It does not authorize knowledge repair, source changes, Unity execution, Git operations, publication, or release acceptance. Missing or stale bindings are blocked.
