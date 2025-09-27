namespace RZPrime.DTOs.Contracts
{
    public static class TokenForwardSaleAbi
    {
        public const string Value = @"
    [
    {
        ""type"": ""function"",
        ""name"": ""DROP_ORDER_TYPEHASH"",
        ""inputs"": [],
        ""outputs"": [
            {
                ""name"": """",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
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
        ""name"": ""eip712Domain"",
        ""inputs"": [],
        ""outputs"": [
            {
                ""name"": ""fields"",
                ""type"": ""bytes1"",
                ""internalType"": ""bytes1""
            },
            {
                ""name"": ""name"",
                ""type"": ""string"",
                ""internalType"": ""string""
            },
            {
                ""name"": ""version"",
                ""type"": ""string"",
                ""internalType"": ""string""
            },
            {
                ""name"": ""chainId"",
                ""type"": ""uint256"",
                ""internalType"": ""uint256""
            },
            {
                ""name"": ""verifyingContract"",
                ""type"": ""address"",
                ""internalType"": ""address""
            },
            {
                ""name"": ""salt"",
                ""type"": ""bytes32"",
                ""internalType"": ""bytes32""
            },
            {
                ""name"": ""extensions"",
                ""type"": ""uint256[]"",
                ""internalType"": ""uint256[]""
            }
        ],
        ""stateMutability"": ""view""
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
                ""internalType"": ""struct RZPrimeSale.Order"",
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
                        ""name"": ""payAmount"",
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
                        ""internalType"": ""enum RZPrimeSale.OrderStatus""
                    }
                ]
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
                ""name"": ""payAmount"",
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
                ""name"": ""payAmount"",
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
