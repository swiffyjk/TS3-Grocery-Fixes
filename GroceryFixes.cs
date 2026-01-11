using System.Collections.Generic;
using Sims3.Gameplay.Actors;
using Sims3.SimIFace;
using Sims3.UI;
using Sims3.Gameplay.Objects.FoodObjects;
using Sims3.Gameplay.Abstracts;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Objects;
using Sims3.Gameplay.Skills;
using MonoPatcherLib;
using System.Reflection;
using System;

namespace swiffyMisc.GroceryFixes
{
    public class Instantiator
    {
        [Tunable, TunableComment("Scripting Mod Instantiator, value does not matter, only its existence")]
        protected static bool kInstantiator = false;
    }

    [Plugin]
    public class ShopByRecipeFixes
    {
        [Tunable, TunableComment("Fixes the bug where households with Sims who haven't gained any Cooking skills don't see anything in the 'Shop by Recipe' tab when Shopping for Groceries. True or False.")]
        public static bool kFixEmptyShopByRecipe = true;

        [Tunable, TunableComment("Adds the recipes of quick meals, provided by Ani's No Fridge Shopping mod, to the 'Shop by Recipe' tab when Shopping for Groceries. If Ani's No Fridge Shopping is not installed, the quick meals simply will not be added and there won't be any errors. True or False.")]
        public static bool kAddQuickMealsToShopByRecipe = true;

        [ReplaceMethod(typeof(RecipeShoppingRabbitHole), "CreateRecipeItemList")]
        public static List<IShoppingUIRecipe> CreateRecipeItemList_GroceryFixPatched(Sim customer, Dictionary<string, List<StoreItem>> storeItems, Dictionary<IngredientData, StoreItem> recipeIngredientMap, List<IShopItem> loadedShoppingCart, List<string> savedShoppingCart, float percentPriceModifier, float salePercentage, int markupPercentage)
        {
            List<IShoppingUIRecipe> list = new List<IShoppingUIRecipe>();
            Dictionary<string, ShoppingCoupon> dictionary = ShoppingRabbitHole.CreateItemToCouponMap(customer);
            Dictionary<string, IShoppingUIRecipe> dictionary2 = new Dictionary<string, IShoppingUIRecipe>();
            Dictionary<Recipe, bool> dictionary3 = new Dictionary<Recipe, bool>();
            foreach (Sim sim in customer.Household.Sims)
            {
                Cooking skill = sim.SkillManager.GetSkill<Cooking>(SkillNames.Cooking);
                if (skill != null)
                {
                    foreach (string text in skill.KnownRecipes)
                    {
                        Recipe recipe;
                        Recipe.NameToRecipeHash.TryGetValue(text, out recipe);
                        if (recipe != null && recipe.CanBuyAllIngredientsFromStore && !dictionary3.ContainsKey(recipe))
                        {
                            bool flag = GameUtils.IsInstalled(recipe.CodeVersion) && GameUtils.IsInstalled(recipe.ModelCodeVersion);
                            if (recipe.IsPetFood && !GameUtils.IsInstalled(ProductVersion.EP5))
                            {
                                flag = false;
                            }
                            if (flag)
                            {
                                dictionary3.Add(recipe, true);
                            }
                        }
                    }
                }
                else
                {
                    if (kFixEmptyShopByRecipe)
                    {
                        foreach (Recipe recipe in Recipe.Recipes) // Patched code, checks all recipes
                        {
                            if (recipe != null && recipe.CanBuyAllIngredientsFromStore && !dictionary3.ContainsKey(recipe))
                                if (recipe.CookingSkillLevelRequired == 0 && recipe.LearnWhenReachCorrectLevel && !(recipe.IsPetFood && !GameUtils.IsInstalled(ProductVersion.EP5)))
                                {
                                    dictionary3.Add(recipe, true);
                                }
                        }
                    }
                }
            }
            if (kAddQuickMealsToShopByRecipe)
            {
                IEnumerable<string> AniSnackRequirements = null;

                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies()) // Uses reflection to find Ani's No Fridge Shopping mod without causing errors if it's not installed
                {
                    Type t = asm.GetType("ani_GroceryShopping.AddMenuItem");
                    if (t == null) continue;

                    FieldInfo fi = t.GetField(
                        "SnackRequirements",
                        BindingFlags.Public | BindingFlags.Static);

                    if (fi != null)
                    {
                        AniSnackRequirements = fi.GetValue(null) as IEnumerable<string>;
                        break;
                    }
                }
                if (AniSnackRequirements != null)
                {
                    char[] array = new char[] { ':' };
                    foreach (string text in AniSnackRequirements)
                    {
                        string[] array2 = text.Split(array, 2);
                        if (array2.Length != 2) continue;
                        string recipeKey = array2[0].Trim();
                        string ingredientKey = array2[1].Trim();
                        Recipe recipe;
                        if (Recipe.NameToRecipeHash.TryGetValue(recipeKey, out recipe))
                        {
                            if (recipe.Ingredient1 == null)
                            {
                                recipe.mNonPersistableData.mIngredient1 = recipe.InitIngredient(ingredientKey);
                            }
                            else
                            {
                                if (!dictionary3.ContainsKey(recipe) && recipe.CanBuyAllIngredientsFromStore)
                                {
                                    dictionary3.Add(recipe, true);
                                }
                            }
                        }
                    }
                }
            }
            bool flag2 = customer.HasTrait(TraitNames.Vegetarian);
            new Dictionary<string, IShoppingUIItem>();
            foreach (KeyValuePair<Recipe, bool> keyValuePair in dictionary3)
            {
                Recipe key = keyValuePair.Key;
                List<IShoppingUIItem> list2 = new List<IShoppingUIItem>();
                foreach (IngredientData ingredientData in key.Ingredients.Keys)
                {
                    int num = key.Ingredients[ingredientData];
                    IngredientData ingredientData2 = ingredientData;
                    if (ingredientData.IsAbstract)
                    {
                        ingredientData2 = IngredientData.GetCheapestIngredientOfAbstractType(ingredientData.Key, false);
                    }
                    for (int i = 0; i < num; i++)
                    {
                        StoreItem storeItem = recipeIngredientMap[ingredientData2];
                        ShoppingUIItem shoppingUIItem = ShoppingRabbitHole.CreateUIItemFromStoreItem(storeItem, percentPriceModifier, salePercentage, markupPercentage, dictionary);
                        list2.Add(shoppingUIItem);
                    }
                }
                string text2 = (flag2 ? key.GenericVegetarianName : key.GenericName);
                ThumbnailKey thumbnailKey = key.GetThumbnailKey(ThumbnailSize.Medium);
                ShoppingUIRecipe shoppingUIRecipe = new ShoppingUIRecipe(text2, list2, "recipe_" + key.Key, thumbnailKey);
                list.Add(shoppingUIRecipe);
                if (!dictionary2.ContainsKey(shoppingUIRecipe.StoreUIItemID))
                {
                    dictionary2.Add(shoppingUIRecipe.StoreUIItemID, shoppingUIRecipe);
                }
            }
            if (savedShoppingCart != null)
            {
                foreach (string text3 in savedShoppingCart)
                {
                    IShoppingUIRecipe shoppingUIRecipe2;
                    if (dictionary2.TryGetValue(text3, out shoppingUIRecipe2))
                    {
                        loadedShoppingCart.Add(shoppingUIRecipe2);
                    }
                }
            }
            return list;
        }
    }
}
