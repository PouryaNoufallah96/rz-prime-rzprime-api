namespace RZPrime.DTOs.Contracts
{
    public static class TokenForwardSaleAbi
    {
        public const string Value = @"
    [
    {
        ""type"": ""function"",
        ""name"": ""ORACLE"",
        ""inputs"": [],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""address"",
                ""internalType"": ""contract IRZUSDTwapOracle""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""batchDropOrderBySig"",
        ""inputs"": [
            {
                ""name"": ""users"",
                ""type"": ""address[]"",
                ""internalType"": ""address[]""
            },
            {
                ""name"": ""orderIds"",
                ""type"": ""string[]"",
                ""internalType"": ""string[]""
            },
            {
                ""name"": ""signatures"",
                ""type"": ""bytes[]"",
                ""internalType"": ""bytes[]""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""batchExpireOrder"",
        ""inputs"": [
            {
                ""name"": ""users"",
                ""type"": ""address[]"",
                ""internalType"": ""address[]""
            },
            {
                ""name"": ""orderIds"",
                ""type"": ""string[]"",
                ""internalType"": ""string[]""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""dropOrderBySig"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            },
            {
                ""name"": ""signature"",
                ""type"": ""bytes"",
                ""internalType"": ""bytes""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""executeOrder"",
        ""inputs"": [
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""expireOrder"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""getOrder"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            }
        ],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""tuple"",
                ""internalType"": ""struct RZPrime.Order"",
                ""components"": [
                    {
                        ""name"": ""buyToken"",
                        ""type"": ""address"",
                        ""internalType"": ""address""
                    },
                    {
                        ""name"": ""tokenAmount"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""payUsdValue"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""endAt"",
                        ""type"": ""uint256"",
                        ""internalType"": ""uint256""
                    },
                    {
                        ""name"": ""status"",
                        ""type"": ""uint8"",
                        ""internalType"": ""enum RZPrime.OrderStatus""
                    }
                ]
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""operator"",
        ""inputs"": [],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""address"",
                ""internalType"": ""address""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""orders"",
        ""inputs"": [
            {
                ""name"": """",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": """",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ],
        ""outputs"": [
            {
                ""name"": ""buyToken"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""tokenAmount"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""payUsdValue"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""endAt"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""status"",
                ""type"": ""uint8"",
                ""internalType"": ""enum RZPrime.OrderStatus""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""paused"",
        ""inputs"": [],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""bool"",
                ""internalType"": ""bool""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""previewPaymentAmount"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            }
        ],
        ""outputs"": [
            {
                ""name"": ""rzusdAmount"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""registerUserOrder"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""internalType"": ""string""
            },
            {
                ""name"": ""buyToken"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""tokenAmount"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""payUsdValue"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""endAt"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""outputs"": [],
        ""stateMutability"": ""nonpayable""
    },
    {
        ""type"": ""function"",
        ""name"": ""reservedTokenAmounts"",
        ""inputs"": [
            {
                ""name"": ""token"",
                ""type"": ""address"",
                ""internalType"": ""address""
            }
        ],
        ""outputs"": [
            {
                ""name"": ""amounts"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""function"",
        ""name"": ""vault"",
        ""inputs"": [],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""address"",
                ""internalType"": ""address""
            }
        ],
        ""stateMutability"": ""view""
    },
    {
        ""type"": ""event"",
        ""name"": ""OrderDropped"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""indexed"": false,
                ""internalType"": ""string""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""OrderExecuted"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""indexed"": false,
                ""internalType"": ""string""
            },
            {
                ""name"": ""rzusdPaid"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""usdValue"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""OrderExpired"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""indexed"": false,
                ""internalType"": ""string""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""OrderRegistered"",
        ""inputs"": [
            {
                ""name"": ""user"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            },
            {
                ""name"": ""orderId"",
                ""type"": ""string"",
                ""indexed"": false,
                ""internalType"": ""string""
            },
            {
                ""name"": ""tokenAmount"",
                ""type"": ""uint256"",
                ""indexed"": false,
                ""internalType"": ""uint256""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""Paused"",
        ""inputs"": [
            {
                ""name"": ""account"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""event"",
        ""name"": ""Unpaused"",
        ""inputs"": [
            {
                ""name"": ""account"",
                ""type"": ""address"",
                ""indexed"": false,
                ""internalType"": ""address""
            }
        ],
        ""anonymous"": false
    },
    {
        ""type"": ""error"",
        ""name"": ""ECDSAInvalidSignature"",
        ""inputs"": []
    },
    {
        ""type"": ""error"",
        ""name"": ""ECDSAInvalidSignatureLength"",
        ""inputs"": [
            {
                ""name"": ""length"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            }
        ]
    },
    {
        ""type"": ""error"",
        ""name"": ""ECDSAInvalidSignatureS"",
        ""inputs"": [
            {
                ""name"": ""s"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            }
        ]
    },
    {
        ""type"": ""error"",
        ""name"": ""EnforcedPause"",
        ""inputs"": []
    },
    {
        ""type"": ""error"",
        ""name"": ""ExpectedPause"",
        ""inputs"": []
    },
    {
        ""type"": ""error"",
        ""name"": ""InvalidShortString"",
        ""inputs"": []
    },
    {
        ""type"": ""error"",
        ""name"": ""OwnableInvalidOwner"",
        ""inputs"": [
            {
                ""name"": ""owner"",
                ""type"": ""address"",
                ""internalType"": ""address""
            }
        ]
    },
    {
        ""type"": ""error"",
        ""name"": ""OwnableUnauthorizedAccount"",
        ""inputs"": [
            {
                ""name"": ""account"",
                ""type"": ""address"",
                ""internalType"": ""address""
            }
        ]
    },
    {
        ""type"": ""error"",
        ""name"": ""ReentrancyGuardReentrantCall"",
        ""inputs"": []
    },
    {
        ""type"": ""error"",
        ""name"": ""SafeERC20FailedOperation"",
        ""inputs"": [
            {
                ""name"": ""token"",
                ""type"": ""address"",
                ""internalType"": ""address""
            }
        ]
    },
    {
        ""type"": ""error"",
        ""name"": ""StringTooLong"",
        ""inputs"": [
            {
                ""name"": ""str"",
                ""type"": ""string"",
                ""internalType"": ""string""
            }
        ]
    }
]

    ";

        public const string ERC20Abi = @"[
    { 'constant':true,'inputs':[{'name':'_owner','type':'address'}],'name':'balanceOf','outputs':[{'name':'balance','type':'uint256'}],'type':'function' },
    { 'constant':true,'inputs':[],'name':'decimals','outputs':[{'name':'','type':'uint8'}],'type':'function' },
    { 'constant':true,'inputs':[],'name':'symbol','outputs':[{'name':'','type':'string'}],'type':'function' }
]";

    }
}
